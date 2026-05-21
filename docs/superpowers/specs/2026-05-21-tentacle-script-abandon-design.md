# Tentacle script abandon — design

**Status:** Draft — both implementation options preserved for external review.
**Ticket:** [EFT-3295](https://linear.app/octopus/issue/EFT-3295/tentacle-script-abandonment-to-release-the-mutex)
**ADR:** [ADR-042 — Defer server-task Abandoned state](https://github.com/OctopusDeploy/adr/pull/226)
**Parallel work:** Server-side (ProcessExecution layer) is being designed in a separate session and will consume the contract proposed here.

---

## Problem

When a Tentacle script is hung in a way that resists `Process.Kill` (Philips' case: PowerShell stuck inside CrowdStrike + Rapid7 fighting over the same process; kernel-level uninterruptible wait), today's flow ends with:

- `ScriptIsolationMutex` stays held → subsequent deployments to that Tentacle queue forever.
- The .NET threadpool thread inside `RunningScript.Execute()` stays parked on `process.WaitForExit()` (synchronous).
- The customer's only recovery is RDP-in-and-kill or reboot. Not acceptable for Philips.

Server-side will detect that cancellation hasn't propagated within its own timeout and will tell Tentacle to **abandon** the script. Tentacle releases the mutex, logs honestly, accepts new work. The runaway OS process is **not** killed — explicitly out of scope per the ticket.

## Scope

In scope:
- `IScriptServiceV2` only (Listening + Polling Tentacles).
- New Halibut RPC verb `AbandonScript`, new exit code `AbandonedExitCode = -48`.
- Behind a feature flag (`Tentacle.AbandonScriptEnabled`) for the first release.

Out of scope:
- SSH targets (different lock model; ticket explicitly defers).
- Kubernetes agent (`IKubernetesScriptServiceV1`): different mechanism, separate stuck-pod work already in flight (`KubernetesPendingPodWatchDog`). Server's capability negotiation handles "don't try abandon on Kubernetes targets" cleanly.
- Old `IScriptService` (V1): no signal that any active Tentacle still negotiates V1.
- Killing the runaway OS process.
- Server-task Abandoned UI state — deferred by ADR-042; task continues to surface as Cancelled.

## Section 1 — Contract surface (locked)

Add a method to existing `IScriptServiceV2`. Do NOT introduce V3 — the convention here is method-addition + capability negotiation.

```csharp
// source/Octopus.Tentacle.Contracts/ScriptServiceV2/IScriptServiceV2.cs
public interface IScriptServiceV2
{
    ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command);
    ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request);
    ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command);
    ScriptStatusResponseV2 AbandonScript(AbandonScriptCommandV2 command);  // NEW
    void CompleteScript(CompleteScriptCommandV2 command);
}

// NEW: source/Octopus.Tentacle.Contracts/ScriptServiceV2/AbandonScriptCommandV2.cs
public class AbandonScriptCommandV2
{
    public AbandonScriptCommandV2(ScriptTicket ticket, long lastLogSequence) { /* … */ }
    public ScriptTicket Ticket { get; }
    public long LastLogSequence { get; }
}
```

**Capability advertisement.** Tentacle's `CapabilitiesServiceV2` advertises an `AbandonScriptV2` capability **iff** the feature flag is on. Server's existing `BackwardsCompatibleAsyncCapabilitiesV2Decorator` mechanism handles "Tentacle doesn't advertise it → don't call it". This is also how the Tentacle-side feature flag works: flag off → no capability advertised → server never calls Abandon for that Tentacle.

**Why a new verb (not a "force" flag on Cancel).** Different semantics: Cancel = "try to stop the OS process gracefully". Abandon = "give up tracking; release the mutex; the OS process may still be running". Two verbs map cleanly to ProcessExecution's two-step escalation (cancel first, abandon if cancel doesn't propagate).

## Section 2 — Mutex release mechanics (TWO OPTIONS — external input requested)

**The core constraint.** `RunningScript.Execute()` acquires `ScriptIsolationMutex` inside a `using` block that wraps a synchronous call to `SilentProcessRunner.ExecuteCommand`. `ExecuteCommand` blocks on `process.WaitForExit()` (line 143). When `WaitForExit` never returns:
1. The mutex is welded shut (the `using`'s Dispose never runs).
2. The threadpool thread inside `Task.Run(() => Execute())` is parked forever.

Both problems need to be solved. The mutex problem is the ticket's primary deliverable; the parked-thread problem is required so Tentacle doesn't accumulate thread leaks each time the abandon path fires.

Rejected alternatives (documented for the reviewer's benefit):
- **Orphan the Task + release mutex via external Dispose.** Releases mutex but leaks a threadpool worker per abandon. Tentacle eventually starves the threadpool. **Rejected.**
- **Manual `Thread` instead of `Task`.** Same leak problem, just trades threadpool for kernel thread handles + stack memory. **Rejected.**
- **`Thread.Abort` / `Thread.Interrupt` / `TerminateThread` P/Invoke.** No safe managed mechanism to release a thread parked in unmanaged code. `TerminateThread` doesn't unwind stack or release locks — can corrupt Tentacle's own state. **Rejected.**
- **Out-of-process script worker.** Cleanly isolates the stuck-process problem from Tentacle, but is a massive refactor far outside EFT-3295's scope. Worth a separate proposal someday.

The only two real fixes both make the wait itself cancellable:

### Option 1 — Async cancellable wait (`WaitForExitAsync`)

Replace the sync `process.WaitForExit()` with `await process.WaitForExitAsync(abandonToken)`. `SilentProcessRunner.ExecuteCommand` becomes (or gains an async sibling that becomes) async. `RunningScript.RunScript` / `Execute()` become async end-to-end.

**Verified behaviour** (.NET source, `Process.cs:1523-1594`): `WaitForExitAsync` uses a `TaskCompletionSource` driven by either the process's `Exited` event or `cancellationToken.UnsafeRegister(... TrySetCanceled ...)`. When the token fires, the awaiter completes with `OperationCanceledException` independently of whether the OS process has exited. The `WaitUntilOutputEOF` follow-up is bypassed on cancellation. **No thread is parked during the wait.**

**Diff shape (~6 files, async ripple):**
- `Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs`: add `ExecuteCommandAsync(... CancellationToken cancel, CancellationToken abandon)`. New abandon-catch returns `AbandonedExitCode`, writes honest log line via the existing `info` callback.
- `Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs`: `RunScript` → `RunScriptAsync`; ctor takes a second `abandonToken`; `Execute()` already async, just awaits the new path.
- `Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs`: `LaunchShell` passes `abandonToken` from the wrapper. `RunningScriptWrapper` gains `abandonTokenSource`. New `AbandonScriptAsync` method.
- `Octopus.Tentacle.Contracts/ScriptServiceV2/`: new command file, interface method (per Section 1).
- `Octopus.Tentacle.Contracts/ScriptExitCodes.cs`: add `AbandonedExitCode = -48`.
- Capabilities advertisement.

**Tradeoffs:**
- ✅ **Zero threads parked** even during normal script execution (async I/O all the way down).
- ✅ Aligns with the long-term direction if/when the team does an async-everywhere pass on the script pipeline.
- ❌ Larger diff and async ripple — touches the call chain that already has stable test coverage.
- ❌ Async correctness rabbit holes (ConfigureAwait, deadlock potential if any caller `.Result`s the new method).

### Option 2 — Sync cancellable wait (`ManualResetEventSlim`)

Replace `process.WaitForExit()` with `ManualResetEventSlim.Wait()` that's signalled by **either** the process's `Exited` event or the abandon token's registration callback. Everything outside the swap stays synchronous.

```csharp
process.EnableRaisingEvents = true;
using var exited = new ManualResetEventSlim(false);
EventHandler exitedHandler = (_, _) => exited.Set();
process.Exited += exitedHandler;
try
{
    using (cancel.Register(() => DoOurBestToCleanUp(process, error)))   // existing
    using (abandon.Register(() => exited.Set()))                        // NEW
    {
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        exited.Wait();   // replaces WaitForExit()

        if (abandon.IsCancellationRequested && !process.HasExited)
        {
            info("Tentacle has abandoned this script. The underlying script process may still be running on this host.");
            return ScriptExitCodes.AbandonedExitCode;
        }

        // existing cleanup path unchanged from here
        SafelyCancelRead(process.CancelErrorRead, debug);
        SafelyCancelRead(process.CancelOutputRead, debug);
        SafelyWaitForAllOutput(outputResetEvent, cancel, debug);
        SafelyWaitForAllOutput(errorResetEvent, cancel, debug);
        return SafelyGetExitCode(process);
    }
}
finally { process.Exited -= exitedHandler; }
```

**Threading.** During the wait, one thread is parked on `exited.Wait()` — **same cost as today's `WaitForExit()`**. When the abandon token's registration callback runs, it sets the event, `Wait()` returns, the thread is released. Normal-script behaviour is identical to today; only the stuck-script path changes.

**Diff shape (~4 files, no async ripple):**
- `SilentProcessRunner.cs`: one method swap, add `abandon` parameter (additive overload).
- `RunningScript.cs`: ctor takes `abandonToken`, plumbs to `RunScript`. `Execute()` structure unchanged.
- `ScriptServiceV2.cs`: new `AbandonScriptAsync` method, `RunningScriptWrapper` gains `abandonTokenSource`.
- Contracts + exit code + capability (same as Option 1).

**Tradeoffs:**
- ✅ Minimal diff, surgical change to the single blocking primitive.
- ✅ Existing sync call chain preserved — easier to review, easier to feature-flag, lower regression risk.
- ✅ Normal-path performance identical to today (no shape change).
- ❌ One threadpool thread parked per running script during the wait, same as today. Async option avoids this entirely. (Note: this matches current behaviour. We are not regressing; we're just not improving the *normal* case.)
- ❌ Doesn't move the codebase toward async; the next person to do this work has to do Option 1's refactor anyway.

### Decision matrix

| | Option 1 (async) | Option 2 (sync, surgical) |
|---|---|---|
| Threads parked during normal run | 0 | 1 (same as today) |
| Threads parked after abandon | 0 | 0 |
| Files touched | ~6 | ~4 |
| Async correctness risk | Real | None |
| Async-everywhere shape progress | Yes | No |
| Review effort | Higher | Lower |
| Existing sync callers of `ExecuteCommand` | Unaffected (new overload) | Unaffected (additive param) |

**Both options:**
- Release the mutex correctly via the existing `using` exit (no external Dispose surgery needed).
- Release the parked thread on abandon.
- Leave the OS process alone (per ticket).
- Write the honest log line and return `AbandonedExitCode`.

**Recommendation pending external input.** Author's lean: Option 2 for the EFT-3295 ship (smaller blast radius, easier feature-flag rollback). Option 1 is the better long-term shape and is the natural next refactor cycle. Looking for an outside engineer's read.

## Section 3 — State, exit code, log wording (common to both options)

- **Exit code:** `ScriptExitCodes.AbandonedExitCode = -48`. Distinct from `CanceledExitCode (-43)`. Server-side telemetry can tell abandoned from cancelled even though task UI surfaces both as "Cancelled" per ADR-042.
- **State on GetStatus after abandon:** `(ProcessState.Complete, AbandonedExitCode, latestLogs)`. Same shape as Cancel returns today.
- **Honest log line:** `"Tentacle has abandoned this script. The underlying script process may still be running on this host."` Written once, into the workspace script log, near the end of the abandon path.
- **Workspace cleanup on subsequent `CompleteScript`:** best-effort. `workspace.Delete` is wrapped in try/catch; failure logs a `Warn` to systemLog and leaks the directory. Justified by feature-flag gating and low expected frequency. Periodic janitor is a future option if signal arrives.
- **Idempotency — actual-status return (NOT silent no-op):**
  - Abandon called twice on the same already-abandoned ticket → returns the cached `(Complete, AbandonedExitCode, logs)` response.
  - Abandon called on a ticket that completed naturally before the abandon arrived (race case the server-side session flagged) → returns `(Complete, realExitCode, logs)` with the **real exit code**, distinct from `AbandonedExitCode`. The server uses this distinction to log *"Script had already completed before abandon was needed"* instead of *"Tentacle abandoned the script"*. Silent no-op would hide this signal.
  - Abandon called on an unknown ticket (never started, or already cleaned up via `CompleteScript`) → returns `(Complete, UnknownScriptExitCode, [])`, matching Cancel's behaviour for the same case.
- **Race with natural completion:** the wrapper's existing `StartScriptMutex` (or a new dedicated lock) serialises abandon entry. If state is already Complete, abandon returns the cached status per the rules above.

## Section 4 — Automated test strategy

### 4.1 `SilentProcessRunner` unit tests (both options)

Style: matches existing `SilentProcessRunnerFixture.cs`. Use short-lived helper scripts/exes (`Thread.Sleep`-equivalent) as process subjects.

| Test | Trigger | Verify |
|---|---|---|
| Normal exit | Run a process that exits 0 | Returns 0; no abandon log line captured by the `info` callback spy. |
| Cancel kills process | Long-running process; fire cancel token | Within 1s: process is killed (`process.HasExited == true`), return value is the kill-induced exit code (Linux: 137; Windows: process-defined). No abandon log line. |
| Abandon while running | Long-running process; fire abandon token | Within ~100ms: returns `AbandonedExitCode`, `info` callback received exactly one call containing "Tentacle has abandoned this script". Then assert `process.HasExited == false` and clean up by killing externally. |
| Abandon AFTER natural exit (race) | Process that exits in ~50ms; fire abandon token at the moment exit fires | Return value is the process's real exit code, not `AbandonedExitCode`. No abandon log line. Verifies the `if (abandon.IsCancellationRequested && !process.HasExited)` guard. |
| Both tokens fire | Long-running process; fire cancel; while cancel.Register is mocked to no-op, fire abandon | `info` callback gets abandon log line; return value is `AbandonedExitCode`. Verifies the unkillable-cancel + abandon escalation path that the integration tests then exercise end-to-end. |

**Option 1 specific:** `WaitForExitAsync(token)` returns within ~50ms of cancellation. **Test verification:** wrap the await in `Stopwatch.StartNew()`; assert elapsed < 100ms. Proves async wait is independent of process exit.

**Option 2 specific:** `ManualResetEventSlim.Wait()` returns within ~50ms of abandon callback. **Test verification:** same `Stopwatch` approach. **Plus thread-leak test:** start 50 stuck processes via `ExecuteCommand` on dedicated threads, fire abandon on all; capture `Process.GetCurrentProcess().Threads.Count` before and 1s after; assert delta ≤ 5 (allow for threadpool jitter).

### 4.2 `ScriptServiceV2` service-layer tests (both options)

Style: matches existing service-layer fixtures using in-memory script shells and stub workspace factories.

| Test | Trigger | Verify |
|---|---|---|
| **Mutex release (load-bearing)** | Start `FullIsolation` script; abandon it; immediately start second `FullIsolation` script | Second `StartScript` returns with `State == Running` within 1s. Reading `ScriptIsolationMutex.TaskLock.Report()` between abandon and second-start shows the lock free in that window. |
| Abandon before StartScript | Call AbandonScript with a ticket never seen | Returns `(Complete, UnknownScriptExitCode)`. Matches existing Cancel behaviour for unknown ticket. |
| Abandon after CompleteScript | Start → Complete → Abandon | Returns `(Complete, UnknownScriptExitCode)` (wrapper already removed; stateStore gone). |
| Abandon then Cancel | Abandon, then Cancel same ticket | Cancel returns the cached abandoned response unchanged. Asserts via response equality. |
| **Cancel then Abandon (real flow)** | Long-running script; cancel; cancel.Register no-op'd to simulate unkillable; abandon | Final GetStatus returns `(Complete, AbandonedExitCode, logs)`. Log content includes the honest line. Subsequent same-ticket StartScript returns the cached state. |
| Abandon during StartScript launch | Concurrent: StartScript holding `StartScriptMutex`, AbandonScript called | Abandon serialises behind StartScript via the existing wrapper mutex. Final state is consistent (no half-abandoned wrapper). |
| Capability advertisement | Feature flag toggled at startup | With flag on, `CapabilitiesServiceV2.GetCapabilities()` response includes `AbandonScriptV2`. With flag off, capability is absent. |
| Capability gating | Flag off; client calls AbandonScript anyway | Returns a "feature disabled" response shape (exact shape TBD with server session). Server side, no decorator should attempt the call when capability is missing. |

### 4.3 Integration tests (real shells, real processes)

Style: matches `Octopus.Tentacle.Tests.Integration/ScriptServiceV2Fixture.cs`.

| Test | Trigger | Verify |
|---|---|---|
| PowerShell + abandon (kill works) | Real PowerShell, `Start-Sleep -Seconds 600`, fire Cancel, normal kill path | Final response is `(Complete, CanceledExitCode)` via the existing path. **Negative check:** abandon log line is NOT present. Confirms we haven't regressed Cancel by accidentally hitting the abandon path. |
| PowerShell + abandon (kill mocked off) | Real PowerShell, sleep; `Hitman` mocked to no-op; fire Cancel; wait; fire AbandonScript | Within 2s of abandon: response is `(Complete, AbandonedExitCode, [...honest log line...])`; mutex is free (verified by starting a second `FullIsolation` script that Acquires within 1s); the real PowerShell process is still alive on the test host (verified via `Process.GetProcessById` outside the test). Test cleanup: kill the leftover PowerShell. |
| **Multi-level-deep hang (ticket-mandated)** | bootstrap → Calamari-shim → user script, with `Hitman` no-op flag set | All verifications from the previous row pass end-to-end through the multi-level launch chain. Confirms abandon works when the stuck process is not the immediate child of Tentacle. |
| Windows workspace cleanup with open handles | Run the abandon path; leave the simulated zombie holding the workspace log file open; call CompleteScript | CompleteScript returns without exception. Tentacle systemLog contains a `Warn` naming the leaked workspace directory. Workspace dir on disk still exists (assert via `Directory.Exists`). No exception bubbles up to the calling test (which simulates Server). |
| Polling Tentacle variant | Configure test fixture as Polling | All verifications from the kill-mocked-off row pass against a Polling Tentacle. |

**Option 1 specific:** End-to-end async path. **Verify:** capture `Process.GetCurrentProcess().Threads.Count` 5s into a stuck-script scenario; assert no thread parked attributable to the script pipeline (use named threads or stack-walk via ETW if precise attribution needed). Most reliable proxy: total thread count not higher than baseline + epsilon.

**Option 2 specific:** **Normal-path timing regression check.** Run a 100-iteration benchmark of normal short-script execution (`Write-Host "x"`); compare median wall-clock time vs. a baseline build without the changes. **Verify:** median delta < 5% (the MRES wait is functionally equivalent to WaitForExit; we don't expect measurable regression).

### 4.4 Feature flag verification

Two flags, both default ON, either kills the feature. Tentacle-side flag governs capability advertisement (the primary gate). Server-side flag `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` governs whether server schedules the delayed dispatch at all. Tests here cover the Tentacle-side flag.

| Test | Trigger | Verify |
|---|---|---|
| Flag off, capability absent | Restart Tentacle with `AbandonScriptEnabled=false`; call `CapabilitiesServiceV2.GetCapabilities()` | Response list does NOT contain `AbandonScriptV2`. |
| Flag off, AbandonScript called (defensive fallback) | Same Tentacle; client invokes AbandonScript directly via Halibut (bypassing capability check) | Response is a "feature disabled" error response. Tentacle systemLog records the disabled-call attempt. **Pass:** no exception, no state mutation. This path is exercised only by a stale capability cache on the server side; production flow goes through the capability check first. |
| Flag off, existing paths unchanged | Restart with flag off; run an existing `ScriptServiceV2Fixture` test suite | All existing tests pass byte-for-byte. Asserts no accidental change to Cancel/Complete/StartScript paths under the feature-flag boundary. |
| Flag toggle reversible | Restart with flag on, then off, then on; capture capabilities response each time | `AbandonScriptV2` appears/disappears/appears in lockstep with the flag. Confirms the flag is the single source of truth on capability, not a startup-only switch. |

## Section 5 — Manual testing plan

Manual scenarios on a real test Tentacle. All scenarios assume the parallel server-side build is deployed.

### Setup

- Test Octopus Server with EFT-3295 server-side build.
- Windows Tentacle (primary) + Linux Tentacle (smoke).
- Debug Tentacle build with `Tentacle.Debug.DisableProcessKill=true` making `Hitman.TryKillProcessAndChildrenRecursively` a no-op — simulant for "kill doesn't work" without engineering real kernel-level waits.
- Feature flag `Tentacle.AbandonScriptEnabled` exposed via config.

### Where to find things (reference for verification steps below)

- **Tentacle systemLog (Windows):** `C:\Octopus\Logs\OctopusTentacle.txt` (or whatever the test instance is configured with — confirm via `Tentacle show-configuration`).
- **Tentacle systemLog (Linux):** `/etc/octopus/<instance>/Logs/OctopusTentacle.txt`.
- **Tentacle workspace root:** `<Tentacle.Home>/Work/`. Each script gets a subdirectory named after its `ScriptTicket`. Inside: `bootstrapRunner.log`, `Output.log`, `script.ps1`/`Bootstrap.sh`, the state store file.
- **Script log in UI:** Octopus Server → the task → expand the deployment step. The script log is what the customer sees and is what gets the honest abandon line.
- **Thread count (Windows):** PowerShell `(Get-Process Tentacle).Threads.Count`, or use Process Explorer's Threads tab. Capture before each scenario for a baseline.
- **Thread count (Linux):** `ps -o nlwp= -p $(pgrep -f Tentacle)` returns the LWP (thread) count for the Tentacle process.
- **Capability advertisement:** Tentacle systemLog at startup contains `Negotiated capabilities: [...]` lines and per-connection capability exchanges. Or: temporarily enable Halibut verbose tracing on the server side and inspect the `CapabilitiesResponseV2` payload from this Tentacle.
- **Mutex state in Tentacle log:** grep for `acquiring isolation mutex` / `Lock acquired` / `Releasing lock` lines with the relevant task ID.

### M1 — Regression smoke (flag ON, normal script)

Deploy `Write-Host "hello"; Start-Sleep 5; Write-Host "done"`.

**Verify (all must pass):**
1. Octopus UI task status → **Success** (green tick).
2. Script log in UI shows `hello` and `done`; no abandon line.
3. Tentacle systemLog: `grep "abandon" OctopusTentacle.txt` → zero matches for this task ID.
4. Tentacle systemLog shows the normal acquire/release pair: `grep "<TASK_ID>" OctopusTentacle.txt | grep -E "Lock acquired|Releasing lock"` → both lines present in order.
5. Thread count (sampled 5s after task completes) → within ±2 of pre-test baseline.

### M2 — Cancel still works (flag ON, killable script)

`DisableProcessKill=false`. Deploy `Start-Sleep -Seconds 300`. Wait ~10s. Click **Cancel** in Octopus UI.

**Verify:**
1. UI task status transitions to **Cancelled** within 30s.
2. Tentacle systemLog: `grep "Hitman\|Releasing lock" OctopusTentacle.txt | tail -20` shows the kill attempt followed by mutex release for this task ID.
3. PowerShell process is gone: `Get-Process powershell -ErrorAction SilentlyContinue` returns nothing for the powershell instance that was running the script. (Match by PID captured from Tentacle log at script start.)
4. `grep "abandon" OctopusTentacle.txt` → zero matches for this task ID. Cancel path was used, not abandon.
5. Deploy a second project to the same Tentacle → starts immediately (mutex was released by the normal Cancel path).

### M3 — The Philips scenario (flag ON, unkillable script)

`Tentacle.Debug.DisableProcessKill=true`. Restart Tentacle. Capture thread-count baseline. Deploy `Start-Sleep -Seconds 600`. Note the script's PowerShell PID from the Tentacle log (`grep "Starting powershell" OctopusTentacle.txt | tail -1`). Click **Cancel** after ~10s. Wait for server-side abandon timeout (1–5 min per parallel session config).

**Verify (all must pass; this is the load-bearing scenario):**

1. **Server side called Abandon.** Server log (`OctopusServer.txt`) shows an `AbandonScript` call for this task's ticket, timestamped after the Cancel attempt + the server's abandon timeout. If the parallel session hasn't named the call yet, grep for "abandon" in server log.
2. **Honest log line in the customer-visible task log.** Open the task in Octopus UI → expand the deployment step → confirm the line `Tentacle has abandoned this script. The underlying script process may still be running on this host.` is present in the script log section.
3. **Tentacle systemLog records the abandon path.** `grep -A2 "abandon" OctopusTentacle.txt | tail -30` shows: AbandonScript invocation received, abandon token cancelled, mutex released for this task ID, wrapper removed.
4. **Mutex released — load-bearing check.** Immediately deploy a second project (any trivial script, `Write-Host "ok"`) to the same Tentacle. **Pass:** second deployment starts within 5s. **Fail:** queues indefinitely with "Waiting for the script in task..." message.
5. **Task UI status = Cancelled** (not a new "Abandoned" state — per ADR-042).
6. **Thread count returned to baseline.** Sample 10s after the abandon. **Pass:** within ±2 of baseline. **Fail:** count grew by 1 or more and stays grown.
7. **The PowerShell process is still alive on the host.** `Get-Process -Id <PID>` returns the process. This is the ticket's "we do not kill the runaway" — verify we didn't accidentally start killing it. Kill it manually at end of test for cleanup.
8. **Exit code in the task log = -48 (AbandonedExitCode)** (or whatever surfaces in the Server-side detail view). Distinguishes from `-43` (CanceledExitCode).

### M4 — Repeated abandon (thread-leak check under repetition)

Capture baseline thread count and Tentacle process working-set memory. Run M3 ten times back-to-back (script the loop so each iteration: deploy → cancel → wait for abandon → next).

**Verify:**
1. Sample thread count after each iteration. **Pass:** count stays within ±5 of baseline across all ten runs. **Fail:** monotonic growth — indicates the chosen option's thread-release mechanism is broken.
2. Sample Tentacle working-set memory after each iteration. **Pass:** stays within ~50MB of baseline (some growth from log buffers etc. is expected). **Fail:** grows by more than ~10MB per iteration — indicates Process objects or zombie tasks are being retained.
3. After all ten runs, deploy a normal project. **Pass:** runs normally, no perf degradation.
4. Kill all leftover `powershell.exe` / `sleep` processes manually at end of test.

This scenario most clearly distinguishes Option 1 (zero thread cost per abandon) from Option 2 (one thread released per abandon) from any rejected zombie-thread variant (growth per abandon).

### M5 — Feature flag off (no behaviour change)

Set `Tentacle.AbandonScriptEnabled=false`. Restart Tentacle.

**Verify:**
1. **Capability not advertised.** Tentacle systemLog at startup: `grep "AbandonScriptV2" OctopusTentacle.txt` → zero matches in the capabilities list (the exact capability name will be confirmed with the server session). Alternatively: enable Halibut verbose tracing on the server and inspect the `CapabilitiesResponseV2` payload for this Tentacle — confirm the abandon capability is absent.
2. **Server doesn't call Abandon.** Repeat the M3 setup. Wait for what would have been the server's abandon timeout. Server log: `grep "AbandonScript" OctopusServer.txt` → zero matches for this task ID.
3. **Tentacle stays wedged.** Confirms today's behaviour is preserved. Subsequent deployment to this Tentacle queues with "Waiting for the script in task..." — verify by attempting a deploy and observing the queue message.
4. Recovery: restart Tentacle (the existing workaround). Verify subsequent deployments work again.

### M6 — Workspace cleanup with open handles (Windows-specific)

Run M3 to completion. Note the script's `ScriptTicket` from the Tentacle log.

**Verify:**
1. **Workspace dir still exists.** `dir <Tentacle.Home>\Work\<TICKET_ID>` returns a directory listing with log files present. The zombie process (or our retained Process object, depending on option chosen) holds open file handles preventing deletion.
2. **systemLog records the failure.** `grep -i "workspace\|delete" OctopusTentacle.txt | grep <TICKET_ID>` shows a `Warn`-level entry naming the directory that could not be deleted, with the underlying I/O exception message.
3. **No propagated exception to Server.** `CompleteScript` returns normally; Server log shows successful completion of the task. **Pass:** no error response from Tentacle, no retry storm in server log.
4. **Tentacle continues to function.** Deploy a third project (not to the wedged workspace). **Pass:** runs normally.
5. **Manual cleanup of leaked workspace works after the zombie process is killed.** Kill the PowerShell process manually; `rmdir /s /q <workspace path>` should now succeed. Confirms the leak is bounded (would be reclaimed if we ever added a janitor).

### M7 — Polling Tentacle variant

Register a Polling Tentacle against the test server. Repeat M3 setup and execution.

**Verify:**
1. All M3 verification points pass with no Polling-specific differences. The Halibut RPC path is the same — only connection initiation direction differs.
2. **Polling-specific check:** during the abandon, Tentacle's polling loop continues. `grep "Polling" OctopusTentacle.txt | tail -20` shows polling activity through the abandon and after. **Pass:** polling not blocked by the abandon flow.
3. After abandon, the Polling Tentacle picks up the next deployment from the server. **Pass:** new deployment dispatched and runs (mutex released).

### M8 — Linux smoke

On a Linux Tentacle, deploy a Bash script: `sleep 600`. Repeat M2 (kill works) and M3 (kill mocked off).

**Verify:**
1. **M2 on Linux:** `ps -p <PID>` shows the bash/sleep process gone after Cancel. Tentacle systemLog shows `Hitman` kill path used. Same outcomes as Windows M2.
2. **M3 on Linux:** all M3 verification points pass. Thread count via `ps -o nlwp= -p $(pgrep -f Tentacle)`. Workspace location: `/etc/octopus/<instance>/Work/<TICKET_ID>/`.
3. **Linux file-handle behaviour differs:** unlike Windows, Linux generally allows deletion of files held open by other processes (the inode survives until the last handle closes). For M6's workspace-cleanup analogue on Linux, the workspace deletion is more likely to succeed even with the zombie process running. Note in test result.
4. Confirms the implementation isn't accidentally Windows-only and behaves sensibly on Linux's different file-handle semantics.

### M9 — Server escalation ordering

Server escalation is hardcoded at **2 minutes** post-Cancel for the first release (`AbandonTentacleScriptOnCancellationTimeoutFeatureToggle`'s timeout constant). Not configurable in production; ask the server-side session for a debug-build override constant if you want to run this faster in your test environment.

**Verify the killable case (no escalation expected):**
1. Run M2 (killable script + cancel). Wait at least 3 minutes.
2. Server log: `grep "AbandonScript" OctopusServer.txt | grep <TICKET_ID>` → **zero matches.** Cancel succeeded inside the 2-minute window; server correctly did not escalate.
3. Tentacle log: zero abandon entries for this task ID.

**Verify the unkillable case (escalation expected):**
4. Run M3 (kill mocked off + cancel). Wait through the 2-minute timeout (use a stopwatch).
5. Server log: `grep "AbandonScript" OctopusServer.txt | grep <TICKET_ID>` → **exactly one match,** timestamped approximately 2 minutes after the Cancel.
6. Tentacle log: one abandon entry for this task ID.

**Verify the actual-status race case** (server-side session's idempotency concern):
7. Set up M3, but let the script complete naturally just before the 2-minute timer fires (use a script that runs ~110 seconds).
8. Server fires AbandonScript anyway because the completion event hasn't reached it yet.
9. Tentacle returns `(Complete, realExitCode, logs)` — NOT `AbandonedExitCode`.
10. Server task log entry: *Script had already completed before abandon was needed.* Confirms the "abandon was unnecessary" signal works end-to-end.

**Bug indicators to flag back to the server session:**
- Server calls AbandonScript on every Cancel (even killable cases) → server's escalation predicate is wrong.
- Server retries AbandonScript multiple times for the same ticket → idempotency on the server side broken.
- Server calls AbandonScript before the 2-minute window → timer is wrong.
- Server calls AbandonScript even with the Tentacle capability missing → capability gating broken; should not have scheduled.

### Sign-off criteria

To turn the feature flag on by default in a future release: M1–M5 pass on Windows; M3 + M4 pass on Linux; M7 passes on Polling; M9 confirms server escalation policy; M6 confirms workspace leak is bounded and logged.

## Risks and rollout

- **Feature flag off by default** for the first release. Customer-by-customer opt-in.
- **Sequence:** after EFT V1 cleanup closes (target end May 2026), before Task Cap 320, targeting Philips' July self-host release.
- **Telemetry:** count of AbandonScript calls per Tentacle per day. Spike = signal that either Cancel is broken or this feature is masking a different bug.
- **Soak test pre-release:** 1000 normal scripts with flag ON, verify no resource leak vs. flag OFF baseline.

## Open questions for external reviewer

1. **Option 1 vs Option 2** for the mutex/thread mechanism. Author's lean: Option 2 for ship, Option 1 as future refactor. Looking for a second opinion. This is the only thing still open after the server-side session alignment below.
2. **Workspace cleanup policy.** Best-effort + leak + log is the proposed default. Should we instead schedule a janitor task? Disk-fill risk is bounded by feature-flag rarity, but a real customer with frequent abandons could accumulate workspaces.

## Coordination — locked with the server-side session (2026-05-21)

Aligned via Linear thread on EFT-3295 (commenter Jim, both sessions). Items below are locked unless explicitly noted.

**Contract (final shape):**

- `ScriptStatusResponseV2 AbandonScript(AbandonScriptCommandV2 command)` on `IScriptServiceV2`.
- `AbandonScriptCommandV2 { ScriptTicket Ticket; long LastLogSequence; }` — same shape as `CancelScriptCommandV2`. Server-side dropped its initial `ServerTaskId` and "cancellation correlation id" proposal; `ScriptTicket` is sufficient.
- Capability name: `AbandonScriptV2`.

**Idempotency (final):** Tentacle returns actual current status. Already-completed script returns `(Complete, realExitCode, logs)` — distinct from `AbandonedExitCode`, so the server's task log entry can record that the abandon was unnecessary. Unknown/already-cleaned-up ticket returns `(Complete, UnknownScriptExitCode, [])`, matching Cancel's existing shape.

**Capability check is the primary gate.** Server uses `BackwardsCompatibleAsyncCapabilitiesV2Decorator` to query `AbandonScriptV2` once per session. Capability absent → server does not schedule the abandon dispatch at all. The RPC-fail-then-log path stays as a defensive fallback for capability-cache staleness, not the primary path.

**Two independent off-switches:**

- Tentacle-side: `Tentacle.AbandonScriptEnabled` (config flag), default ON. Governs capability advertisement.
- Server-side: `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` (server toggle), default ON. Governs whether the server schedules the delayed abandon check at all.
- Either side can disable; both must be on for the feature to fire.

**Escalation timing (locked for first release):** Server schedules the abandon dispatch 2 minutes after Cancel is issued and the script hasn't transitioned to Complete. Hardcoded `public static readonly TimeSpan` on the server toggle class, not configurable. May revisit if Luke's open question to the parallel session ("why not align with Force Cancel?") changes the trigger model.

**Post-abandon flow:**

1. Server calls `AbandonScript` → gets `ScriptStatusResponseV2`.
2. Server publishes `TentacleScriptAbandonedEvent`.
3. Existing post-cancel path proceeds (eventually calls `CompleteScript` downstream).

Server-side will verify the exact GetStatus-poll-vs-read-from-response detail during their implementation plan.

**Task log wording:**

- Tentacle script log (this doc's Section 3): *Tentacle has abandoned this script. The underlying script process may still be running on this host.*
- Server task log (server session's surface). Server session's working proposal:
  - On dispatch: *Cancellation hasn't taken effect on Tentacle after 2 minutes. Abandoning the script to release the script-isolation mutex.*
  - On Tentacle returning `AbandonedExitCode`: *Tentacle abandoned the script.*
  - On Tentacle returning a real exit code (abandon unnecessary): *Script had already completed before abandon was needed.*
- I pushed back on the dispatch wording — "script-isolation mutex" exposes internal terminology to the customer. Suggested rewrite: *Cancellation hasn't taken effect on Tentacle after 2 minutes. Abandoning the script so this target can accept new deployments.* Server session's call which to ship with.
