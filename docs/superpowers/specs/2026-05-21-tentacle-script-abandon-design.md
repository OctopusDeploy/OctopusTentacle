# Tentacle script abandon — design

**Status:** Draft. Implementation approach locked (async). One open question remains (workspace cleanup policy).
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
- Gated by server-side feature flag (`AbandonTentacleScriptOnCancellationTimeoutFeatureToggle`) for the first release. No Tentacle-side flag — capability advertisement is binary on build version.

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

**Capability advertisement.** Tentacle's `CapabilitiesServiceV2` advertises `AbandonScriptV2` once the build supports it. Binary on build version, no Tentacle-side toggle. Server's existing `BackwardsCompatibleAsyncCapabilitiesV2Decorator` handles "Tentacle doesn't advertise it → don't call it" for older Tentacles. Server-side `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` is the only feature-flag off-switch.

**Why a new verb (not a "force" flag on Cancel).** Different semantics: Cancel = "try to stop the OS process gracefully". Abandon = "give up tracking; release the mutex; the OS process may still be running". Two verbs map cleanly to ProcessExecution's two-step escalation (cancel first, abandon if cancel doesn't propagate).

## Section 2 — Mutex release mechanics (locked: async)

**The core constraint.** `RunningScript.Execute()` acquires `ScriptIsolationMutex` inside a `using` block that wraps a synchronous call to `SilentProcessRunner.ExecuteCommand`. `ExecuteCommand` blocks on `process.WaitForExit()` (line 143). When `WaitForExit` never returns:
1. The mutex is welded shut (the `using`'s Dispose never runs).
2. The threadpool thread inside `Task.Run(() => Execute())` is parked forever.

Both problems need to be solved. The mutex problem is the ticket's primary deliverable; the parked-thread problem is required so Tentacle doesn't accumulate thread leaks each time the abandon path fires.

**Rejected alternatives** (documented for the reviewer's benefit):

- **Orphan the Task + release mutex via external Dispose.** Releases mutex but leaks a threadpool worker per abandon. Tentacle eventually starves the threadpool.
- **Manual `Thread` instead of `Task`.** Same leak problem, just trades threadpool for kernel thread handles + stack memory.
- **`Thread.Abort` / `Thread.Interrupt` / `TerminateThread` P/Invoke.** No safe managed mechanism to release a thread parked in unmanaged code. `TerminateThread` doesn't unwind stack or release locks; can corrupt Tentacle's own state.
- **Out-of-process script worker.** Cleanly isolates the stuck-process problem from Tentacle, but is a massive refactor far outside EFT-3295's scope. Worth a separate proposal someday.
- **Sync cancellable wait via `ManualResetEventSlim.Wait()`** (the earlier "Option 2" we held open for external input). Replaces only the blocking primitive inside `SilentProcessRunner`, leaves everything else synchronous. Smaller diff, but preserves a parked thread per running script in the normal case (same cost as today) and doesn't move the codebase toward async. Rejected in favour of the async approach below — direction matters, not just diff size.

### The chosen approach: async cancellable wait

Replace the sync `process.WaitForExit()` with `await process.WaitForExitAsync(abandon)`. **Replace `ExecuteCommand` outright; do NOT ship an additive overload.** Every caller migrates to await.

**Verified behaviour** (.NET source, `Process.cs:1523-1594`): `WaitForExitAsync` uses a `TaskCompletionSource` driven by either the process's `Exited` event or `cancellationToken.UnsafeRegister(... TrySetCanceled ...)`. When the token fires, the awaiter completes with `OperationCanceledException` independently of whether the OS process has exited. The `WaitUntilOutputEOF` follow-up is bypassed on cancellation. **No thread is parked during the wait.**

**Two tokens, one passed to the wait.** `cancel` keeps its existing job (`cancel.Register` fires `DoOurBestToCleanUp` → `Hitman.Kill`). `abandon` is the new signal whose only job is "stop waiting, do not touch the process". Only `abandon` is passed into `WaitForExitAsync`; do NOT link `cancel` in. When `cancel` fires and the kill works, the process exits and the wait returns naturally via the `Exited` event. When `cancel` fires and the kill DOESN'T work (Philips), the wait keeps going until `abandon` fires from the server's 2-minute escalation. Linking `cancel` into the wait token would race the kill against the wait-cancellation and lose the natural-exit code on the happy path.

```csharp
using (cancel.Register(() => DoOurBestToCleanUp(process, error)))
{
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    try
    {
        await process.WaitForExitAsync(abandon).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (abandon.IsCancellationRequested && !process.HasExited)
    {
        info("Tentacle has abandoned this script. The underlying script process may still be running on this host.");
        return ScriptExitCodes.AbandonedExitCode;
    }

    // process exited (naturally or via cancel-triggered kill) — existing cleanup path
    SafelyCancelRead(process.CancelErrorRead, debug);
    SafelyCancelRead(process.CancelOutputRead, debug);
    return SafelyGetExitCode(process);
}
```

**Diff shape — `ExecuteCommand` becomes `ExecuteCommandAsync`, all callers migrate.** Search across the repo found ~20 call sites. Every one updates.

Production code:
- `source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs` — the method itself. Rename, return `Task<int>`, swap `WaitForExit()` for `await WaitForExitAsync(abandon)`. Two-token signature.
- `source/Octopus.Tentacle/Util/ISilentProcessRunner.cs` — interface and the in-process wrapper become async.
- `source/Octopus.Tentacle/Util/CommandLineRunner.cs` — caller migration.
- `source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs` — `RunScript` → `RunScriptAsync`; ctor takes `abandonToken` alongside `runningScriptToken`; `Execute()` awaits the new path.
- `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs` — `LaunchShell` passes `abandonToken` from the wrapper. `RunningScriptWrapper` gains `abandonTokenSource`. New `AbandonScriptAsync` method.
- `source/Octopus.Tentacle.Contracts/ScriptServiceV2/` — new `AbandonScriptCommandV2.cs`, interface method on `IScriptServiceV2.cs` (per Section 1).
- `source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs` — add `AbandonedExitCode = -48`.
- Capabilities advertisement (`AbandonScriptV2`).

Kubernetes integration test scaffolding (all caller-migration, no logic change):
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Tooling/KubeCtlTool.cs`
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/DockerImageLoader.cs` (2 call sites)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesAgentInstaller.cs` (3 call sites)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesClusterInstaller.cs` (4 call sites)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/HelmDownloader.cs`
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/ToolDownloader.cs`

Tentacle integration test scaffolding (caller migration):
- `source/Octopus.Tentacle.Tests.Integration/PowerShellStartupDetectionTests.cs` (3 call sites)
- `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs`
- `source/Octopus.Tentacle.Tests.Integration/Support/TentacleFetchers/LinuxTentacleFetcher.cs`

**What happens to stdout/stderr after abandon.** Returning `AbandonedExitCode` unwinds the method. The outer `using (var process = new Process())` disposes the Process, which closes our end of the redirected pipes. The OS process may get EPIPE on its next stdout/stderr write. This is consistent with the ticket: we're closing our own handles, not killing the runaway process. The script's runtime keeps doing whatever it's doing; many scripts ignore broken-pipe errors, and scripts that fail on them already had nowhere to log anyway. The alternative — leaving the Process and its pipes pinned in memory indefinitely — is the resource-accumulation problem we already rejected.

**Async correctness watch-outs for the implementation plan:**
- Every new async method gets `.ConfigureAwait(false)`.
- No `.Result` / `.Wait()` calls on the new path; if a caller can't easily be made async, surface it for separate handling rather than block-on-async.
- Verify no deadlock under the Tentacle's synchronisation context (none, but worth confirming).

## Section 3 — State, exit code, log wording

- **Exit code:** `ScriptExitCodes.AbandonedExitCode = -48`. Distinct from `CanceledExitCode (-43)`. Server-side telemetry can tell abandoned from cancelled even though task UI surfaces both as "Cancelled" per ADR-042.
- **State on GetStatus after abandon:** `(ProcessState.Complete, AbandonedExitCode, latestLogs)`. Same shape as Cancel returns today.
- **Honest log line:** `"Tentacle has abandoned this script. The underlying script process may still be running on this host."` Written once, into the workspace script log, near the end of the abandon path.
- **Workspace cleanup on subsequent `CompleteScript`:** best-effort. `workspace.Delete` is wrapped in try/catch; failure logs a `Warn` to systemLog and leaks the directory. Justified by the low expected frequency of abandons. Periodic janitor is a future option if signal arrives.
- **Idempotency — actual-status return (NOT silent no-op):**
  - Abandon called twice on the same already-abandoned ticket → returns the cached `(Complete, AbandonedExitCode, logs)` response.
  - Abandon called on a ticket that completed naturally before the abandon arrived (race case the server-side session flagged) → returns `(Complete, realExitCode, logs)` with the **real exit code**, distinct from `AbandonedExitCode`. The server uses this distinction to log *"Script had already completed before abandon was needed"* instead of *"Tentacle abandoned the script"*. Silent no-op would hide this signal.
  - Abandon called on an unknown ticket (never started, or already cleaned up via `CompleteScript`) → returns `(Complete, UnknownScriptExitCode, [])`, matching Cancel's behaviour for the same case.
- **Race with natural completion:** the wrapper's existing `StartScriptMutex` (or a new dedicated lock) serialises abandon entry. If state is already Complete, abandon returns the cached status per the rules above.

## Section 4 — Automated test strategy

### 4.1 `SilentProcessRunner` unit tests

Style: matches existing `SilentProcessRunnerFixture.cs`. Use short-lived helper scripts/exes as process subjects.

| Test | Trigger | Verify |
|---|---|---|
| Normal exit | Run a process that exits 0 | Returns 0; no abandon log line captured by the `info` callback spy. |
| Cancel kills process | Long-running process; fire cancel token | Within 1s: process is killed (`process.HasExited == true`), return value is the kill-induced exit code (Linux: 137; Windows: process-defined). No abandon log line. |
| Abandon while running | Long-running process; fire abandon token | Within ~100ms: returns `AbandonedExitCode`, `info` callback received exactly one call containing "Tentacle has abandoned this script". Then assert `process.HasExited == false` and clean up by killing externally. |
| Abandon AFTER natural exit (race) | Process that exits in ~50ms; fire abandon token at the moment exit fires | Return value is the process's real exit code, not `AbandonedExitCode`. No abandon log line. Verifies the `if (abandon.IsCancellationRequested && !process.HasExited)` guard. |
| Both tokens fire | Long-running process; fire cancel; while cancel.Register is mocked to no-op, fire abandon | `info` callback gets abandon log line; return value is `AbandonedExitCode`. Verifies the unkillable-cancel + abandon escalation path that the integration tests then exercise end-to-end. |

**Async-specific timing assertion:** `WaitForExitAsync(token)` returns within ~50ms of cancellation. **Test verification:** wrap the await in `Stopwatch.StartNew()`; assert elapsed < 100ms. Proves async wait is independent of process exit.

**Thread-leak regression test:** start 50 stuck processes via `ExecuteCommandAsync` (all `await`ed in parallel), fire abandon on all; capture `Process.GetCurrentProcess().Threads.Count` before and 1s after; assert delta ≤ 5 (allow for threadpool jitter). The async path should produce zero parked threads at steady state.

### 4.2 `ScriptServiceV2` service-layer tests

Style: matches existing service-layer fixtures using in-memory script shells and stub workspace factories.

| Test | Trigger | Verify |
|---|---|---|
| **Mutex release (load-bearing)** | Start `FullIsolation` script; abandon it; immediately start second `FullIsolation` script | Second `StartScript` returns with `State == Running` within 1s. Reading `ScriptIsolationMutex.TaskLock.Report()` between abandon and second-start shows the lock free in that window. |
| Abandon before StartScript | Call AbandonScript with a ticket never seen | Returns `(Complete, UnknownScriptExitCode)`. Matches existing Cancel behaviour for unknown ticket. |
| Abandon after CompleteScript | Start → Complete → Abandon | Returns `(Complete, UnknownScriptExitCode)` (wrapper already removed; stateStore gone). |
| Abandon then Cancel | Abandon, then Cancel same ticket | Cancel returns the cached abandoned response unchanged. Asserts via response equality. |
| **Cancel then Abandon (real flow)** | Long-running script; cancel; cancel.Register no-op'd to simulate unkillable; abandon | Final GetStatus returns `(Complete, AbandonedExitCode, logs)`. Log content includes the honest line. Subsequent same-ticket StartScript returns the cached state. |
| Abandon during StartScript launch | Concurrent: StartScript holding `StartScriptMutex`, AbandonScript called | Abandon serialises behind StartScript via the existing wrapper mutex. Final state is consistent (no half-abandoned wrapper). |
| Capability advertisement | Tentacle build with the abandon feature; query `CapabilitiesServiceV2.GetCapabilities()` | Response includes `AbandonScriptV2`. Tentacle builds without the feature do not advertise it. |

### 4.3 Integration tests (real shells, real processes)

Style: matches `Octopus.Tentacle.Tests.Integration/ClientScriptExecutionIsolationMutex.cs` (the closest existing analogue — real Tentacle, real script, mutex semantics under test).

**Timing flakiness: use the existing builders, not raw shell + `Thread.Sleep`.** The integration test suite has stable patterns for this exact class of test:

- `ScriptBuilder` (`Octopus.Tentacle.CommonTestUtils/Builders/ScriptBuilder.cs`) composes cross-platform script bodies. Use `.CreateFile(path)` to signal "script reached this line" and `.WaitForFileToExist(path)` to block the script on an event, not a sleep race. This is how `ClientScriptExecutionIsolationMutex` reliably exercises long-running scripts without `Thread.Sleep` timing assumptions.
- `TestExecuteShellScriptCommandBuilder` (`Octopus.Tentacle.Tests.Integration/Util/Builders/`) composes the script command: `.SetScriptBody(ScriptBuilder)`, `.WithIsolationLevel(...)`, `.WithIsolationMutexName(...)`, `.Build()`.
- `TentacleConfigurationTestCase.CreateBuilder()` and `ClientAndTentacleBuilder` set up real Tentacle + Halibut for the test. Same as existing tests.
- `TentacleServiceDecoratorBuilder.RecordMethodUsages(...)` decorates the script service so the test can assert how many times each method was called. Use this to verify capability negotiation and call counts for the new `AbandonScript` verb.
- `Wait.For(condition, timeout, onFail, ct)` is the event-driven polling helper. Always preferred over `Task.Delay` in test bodies.

**Pattern to follow:** mirror `ClientScriptExecutionIsolationMutex.cs`. Stuck-script tests should use `ScriptBuilder.WaitForFileToExist(...)` as the "kernel-blocked" simulant rather than `sleep 600`. The file-wait is event-driven and the test can release it on demand by creating the file. For the unkillable variant, combine the file-wait pattern with the `Tentacle.Debug.DisableProcessKill` flag described in the manual test setup so `Hitman` becomes a no-op for the test's duration.

| Test | Trigger | Verify |
|---|---|---|
| PowerShell + abandon (kill works) | Real PowerShell, `Start-Sleep -Seconds 600`, fire Cancel, normal kill path | Final response is `(Complete, CanceledExitCode)` via the existing path. **Negative check:** abandon log line is NOT present. Confirms we haven't regressed Cancel by accidentally hitting the abandon path. |
| PowerShell + abandon (kill mocked off) | Real PowerShell, sleep; `Hitman` mocked to no-op; fire Cancel; wait; fire AbandonScript | Within 2s of abandon: response is `(Complete, AbandonedExitCode, [...honest log line...])`; mutex is free (verified by starting a second `FullIsolation` script that Acquires within 1s); the real PowerShell process is still alive on the test host (verified via `Process.GetProcessById` outside the test). Test cleanup: kill the leftover PowerShell. |
| **Multi-level-deep hang (ticket-mandated)** | bootstrap → Calamari-shim → user script, with `Hitman` no-op flag set | All verifications from the previous row pass end-to-end through the multi-level launch chain. Confirms abandon works when the stuck process is not the immediate child of Tentacle. |
| Windows workspace cleanup with open handles | Run the abandon path; leave the simulated zombie holding the workspace log file open; call CompleteScript | CompleteScript returns without exception. Tentacle systemLog contains a `Warn` naming the leaked workspace directory. Workspace dir on disk still exists (assert via `Directory.Exists`). No exception bubbles up to the calling test (which simulates Server). |
| Polling Tentacle variant | Configure test fixture as Polling | All verifications from the kill-mocked-off row pass against a Polling Tentacle. |

**End-to-end async thread audit.** Capture `Process.GetCurrentProcess().Threads.Count` 5s into a stuck-script scenario; assert no thread parked attributable to the script pipeline (use named threads or stack-walk via ETW if precise attribution needed). Most reliable proxy: total thread count not higher than baseline + epsilon.

**Normal-path timing regression check.** Run a 100-iteration benchmark of normal short-script execution (`Write-Host "x"`); compare median wall-clock time vs. a baseline build without the changes. **Verify:** median delta within margin of error. The async swap should not measurably slow normal script execution.

## Section 5 — Manual testing plan

Manual scenarios on a real test Tentacle. All scenarios assume the parallel server-side build is deployed.

### Setup

- Test Octopus Server with EFT-3295 server-side build.
- Windows Tentacle (primary) + Linux Tentacle (smoke).
- Debug Tentacle build with `Tentacle.Debug.DisableProcessKill=true` making `Hitman.TryKillProcessAndChildrenRecursively` a no-op — simulant for "kill doesn't work" without engineering real kernel-level waits.
- Server-side feature flag `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` (default ON, configured on the test Octopus Server).

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

Async should produce zero thread cost per abandon; any growth across runs means the implementation diverged from the design.

### M5 — Server-side flag off (Tentacle behaves as today)

Set the server-side `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` to OFF in the test Octopus Server. Restart Server. Leave Tentacle untouched.

**Verify:**
1. **Server doesn't dispatch Abandon.** Repeat the M3 setup. Wait past the would-be 2-minute escalation point. Server log: `grep "AbandonScript" OctopusServer.txt` → zero matches for this task ID.
2. **Tentacle still advertises the capability.** Optional sanity check via Halibut verbose tracing: `CapabilitiesResponseV2` from this Tentacle still contains `AbandonScriptV2`. The flag lives on the Server, not on Tentacle.
3. **Tentacle stays wedged.** Subsequent deployment to this Tentacle queues with "Waiting for the script in task...". Confirms today's behaviour is preserved when Server has the feature off.
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
- **Soak test pre-release:** 1000 normal scripts with the server-side flag ON, verify no resource leak vs. flag OFF baseline.

## Open questions for external reviewer

1. **Workspace cleanup policy.** Best-effort + leak + log is the proposed default. Should we instead schedule a janitor task? Disk-fill risk is bounded by the rarity of abandons, but a real customer with frequent abandons could accumulate workspaces.

## Coordination — locked with the server-side session (2026-05-21)

Aligned via Linear thread on EFT-3295 (commenter Jim, both sessions). Items below are locked unless explicitly noted.

**Contract (final shape):**

- `ScriptStatusResponseV2 AbandonScript(AbandonScriptCommandV2 command)` on `IScriptServiceV2`.
- `AbandonScriptCommandV2 { ScriptTicket Ticket; long LastLogSequence; }` — same shape as `CancelScriptCommandV2`. Server-side dropped its initial `ServerTaskId` and "cancellation correlation id" proposal; `ScriptTicket` is sufficient.
- Capability name: `AbandonScriptV2`.

**Idempotency (final):** Tentacle returns actual current status. Already-completed script returns `(Complete, realExitCode, logs)` — distinct from `AbandonedExitCode`, so the server's task log entry can record that the abandon was unnecessary. Unknown/already-cleaned-up ticket returns `(Complete, UnknownScriptExitCode, [])`, matching Cancel's existing shape.

**Capability check is the primary gate.** Server uses `BackwardsCompatibleAsyncCapabilitiesV2Decorator` to query `AbandonScriptV2` once per session. Capability absent → server does not schedule the abandon dispatch at all. The RPC-fail-then-log path stays as a defensive fallback for capability-cache staleness, not the primary path.

**One off-switch, server-side:** `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` (default ON). Governs whether server escalates to AbandonScript at all. No Tentacle-side flag — Tentacle's capability advertisement is binary on build version. (Earlier draft had a Tentacle-side flag too; dropped after PR review surfaced that it can't be cleanly toggled at runtime without versioning the service contract.)

**Escalation timing (locked for first release):** 2 minutes. Both V1 and V2 execution pipelines escalate to AbandonScript on their next status-poll once cancellation has been pending that long. Hardcoded on the server toggle class, not configurable. Server-side updated 2026-05-21: trigger switched from a delayed NSB message to a polling-loop check; no new timers on the server side. The Tentacle-side contract is unchanged either way.

**Execution-pipeline scope (server-side, 2026-05-21):** V1 *and* V2 server-side execution pipelines call AbandonScript via the same contract. Philips is V1 self-host so V1 is actually the urgent path. Doesn't change anything Tentacle is building.

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
