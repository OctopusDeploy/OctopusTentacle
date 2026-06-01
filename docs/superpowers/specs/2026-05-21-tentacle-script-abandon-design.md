# Tentacle script abandon — design

**Status:** Draft, ready for implementation planning. Contract aligned with the parallel server-side session.
**Ticket:** [EFT-3295](https://linear.app/octopus/issue/EFT-3295/tentacle-script-abandonment-to-release-the-mutex)
**ADR:** [ADR-042 — Defer server-task Abandoned state](https://github.com/OctopusDeploy/adr/pull/226)
**Parallel work:** Server-side (ProcessExecution layer) is being designed in a separate session and consumes the contract proposed here.

---

## Problem

When a Tentacle script is hung in a way that resists `Process.Kill` (Philips' case: PowerShell stuck inside CrowdStrike + Rapid7 fighting over the same process; kernel-level uninterruptible wait), today's flow ends with:

- `ScriptIsolationMutex` stays held → subsequent deployments to that Tentacle queue forever.
- The .NET threadpool thread inside `RunningScript.Execute()` stays parked on `process.WaitForExit()` (synchronous).
- The customer's only recovery is RDP-in-and-kill or reboot. Not acceptable for Philips.

Server-side detects that cancellation hasn't propagated within its own timeout and tells Tentacle to **abandon** the script. Tentacle stops waiting, releases the mutex, logs honestly, and accepts new work.

**Abandon now best-effort-kills the process** (see ADR-42 reversal below). The runaway OS process survives only when the kill genuinely can't land — exactly the stuck / re-parented-grandchild case the ticket is about.

## Abandon semantics (the agreed model)

- **Cancel** = best-effort kill, then **WAIT** for the script to finish and report its real outcome.
- **Abandon** = best-effort kill, then **DON'T wait** — release the isolation mutex and return `AbandonedExitCode` (-48) even if the script didn't die.

Both attempt the kill. Only abandon stops waiting and releases the mutex. A process survives abandon only when the kill genuinely can't land (stuck / re-parented grandchild). This closes the abuse vector where someone could use abandon to leave a perfectly killable process running unmanaged.

## Scope

In scope:
- `IScriptServiceV2` only (Listening + Polling Tentacles).
- New Halibut RPC verb `AbandonScript`, new exit code `AbandonedExitCode = -48`.
- New optional client parameter `abandonAfterCancellationPendingFor` on `TentacleClient.ExecuteScript`.
- Gated by server-side feature flag (`AbandonTentacleScriptOnCancellationTimeoutFeatureToggle`) for the first release. No Tentacle-side flag — capability advertisement is binary on build version.

Out of scope:
- SSH targets (different lock model; ticket explicitly defers).
- Kubernetes agent (`IKubernetesScriptServiceV1`): different mechanism, separate stuck-pod work already in flight (`KubernetesPendingPodWatchDog`). Server's capability negotiation handles "don't try abandon on Kubernetes targets" cleanly.
- Old `IScriptService` (V1): no signal that any active Tentacle still negotiates V1.
- Server-task Abandoned UI state — deferred by ADR-042; task continues to surface as Cancelled.

## Section 0 — Two PRs, sequenced

The work ships as two PRs in sequence so the async migration is reviewable and mergeable independently of the abandon feature, and so the behavioural change to grandchild handling is gated behind universal server abandon support.

```
main ← PR1 (#1226) ← PR2 (#1244, draft)
```

### PR1 (#1226) — ships first

The wait in `SilentProcessRunner` is a **synchronous `process.WaitForExit()` run on a `Task`**, raced against an abandon signal via `Task.WhenAny`. The abandon signal is a `TaskCompletionSource<object?>` completed when the abandon token fires. (The non-generic `TaskCompletionSource` does NOT exist on net48, so the generic form is required.)

**Cancel behaves exactly like main.** `cancel.Register` runs `DoOurBestToCleanUp`, which does best-effort `Hitman.Kill` **and `process.Close()`**. `Close` releases the redirected pipe handles, so the synchronous wait returns even when a re-parented grandchild holds stdout/stderr open. So cancel — including the grandchild case — is handled exactly as it is today, **with no server dependency**. Cancel of a genuinely un-killable script hangs the wait until abandon fires.

`Close` is safe here precisely because the wait is synchronous; it would race the `Exited` event of the async `WaitForExitAsync`, which PR1 does not use.

PR1 also adds the full abandon contract surface, the client parameter, capability advertisement, and tests.

### PR2 (#1244) — draft, ships only after all servers support abandon

Switches the wait to async `await process.WaitForExitAsync(...)` for performance: no thread is blocked per running script. With the async wait, `Close()` can no longer be used to unblock the grandchild case — it races the `Exited` event the async wait depends on — so `process.Close()` is removed from `DoOurBestToCleanUp`. Consequence: **cancel no longer unsticks a re-parented grandchild on its own; grandchildren require abandon (cancel-then-abandon)**. Cancel returns the real kill exit code instead of -1.

PR2 must ship only after every server in the fleet supports the abandon escalation, because it makes server-side abandon the only recovery path for the grandchild case.

The two grandchild tests flip from "cancel alone does not hang" (PR1) to "cancel then abandon" (PR2).

## Section 1 — Contract surface

Add a method to existing `IScriptServiceV2`. Do NOT introduce V3 — the convention here is method-addition + capability negotiation. The server-side RPC contract below is **unchanged** by the ADR-42 reversal.

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

**Why a new verb (not a "force" flag on Cancel).** Different semantics: Cancel = "best-effort kill, then wait for the real outcome". Abandon = "best-effort kill, then stop waiting; release the mutex; return -48". Two verbs map cleanly to ProcessExecution's two-step escalation (cancel first, abandon if cancel doesn't propagate).

## Section 2 — Where abandon's kill lives, and mutex release

**The core constraint.** `RunningScript.Execute()` acquires `ScriptIsolationMutex` inside a `using` block that wraps the call into `SilentProcessRunner`. That call blocks (PR1) or awaits (PR2) on the process wait. When the wait never returns:
1. The mutex is welded shut (the `using`'s Dispose never runs).
2. In PR1 the threadpool thread is parked; in PR2 no thread is parked, but the awaiter never completes.

Abandon solves both by completing the wait via the abandon signal regardless of whether the OS process exited.

**Where the kill lives.** `ScriptServiceV2.AbandonScriptAsync` calls only `runningScript.Abandon()`. The kill happens in `SilentProcessRunner`'s **abandon branch**: when the abandon token is observed it calls `DoOurBestToCleanUp` (best-effort `Hitman.Kill`, idempotent if cancel already ran it) and then returns `AbandonedExitCode`. Doing the kill there — in the runner, sequentially in the abandon branch — closes the abuse vector for ANY abandon (including a direct RPC with no prior cancel), and is **race-free**.

> **Why not fire the cancel token from `AbandonScriptAsync` (the originally-proposed shape)?** Firing `Cancel()` then `Abandon()` races: cancel's `Hitman.Kill` makes a killable process exit, which resolves the runner's wait to the *cancel* exit code (`-1`) before the abandon token is observed — the runner returns `-1` instead of `-48`. Reversing the order makes `-48` deterministic but then the kill races the runner disposing the process. Doing the kill sequentially inside the abandon branch avoids both races. (Proven by `AbandonScript_WithNoPriorCancel_KillsTheProcess`.)

**Deterministic -48 when both tokens are set.** In PR1's `Task.WhenAny`, key the abandoned result on `abandon.IsCancellationRequested` rather than on which task won the race. PR2's async abandon-catch is already deterministic on `abandon.IsCancellationRequested`.

**PR1 wait shape (synchronous wait raced against abandon):**

```csharp
using (cancel.Register(() => DoOurBestToCleanUp(process, error)))  // Hitman.Kill + process.Close()
{
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    var abandonTcs = new TaskCompletionSource<object?>();
    using (abandon.Register(() => abandonTcs.TrySetResult(null)))
    {
        var waitTask = Task.Run(() => process.WaitForExit());
        await Task.WhenAny(waitTask, abandonTcs.Task).ConfigureAwait(false);

        if (abandon.IsCancellationRequested && !process.HasExited)
        {
            info("Tentacle has abandoned this script. The underlying script process may still be running on this host.");
            SafelyCancelRead(process.CancelErrorRead, debug);
            SafelyCancelRead(process.CancelOutputRead, debug);
            return ScriptExitCodes.AbandonedExitCode;
        }

        // process exited (naturally, or via cancel-triggered Kill+Close releasing the pipes)
        SafelyCancelRead(process.CancelErrorRead, debug);
        SafelyCancelRead(process.CancelOutputRead, debug);
        return SafelyGetExitCode(process);
    }
}
```

**PR2 wait shape (async wait, no `process.Close()`):** the `Task.Run(WaitForExit)` + `WhenAny` is replaced by `await process.WaitForExitAsync(abandon)`, and `process.Close()` is removed from `DoOurBestToCleanUp`. The abandon-catch keys on `abandon.IsCancellationRequested && !process.HasExited` exactly as above. Cancel returns the real kill exit code via the natural `Exited`-event completion.

**Diff shape.** The wait method and its callers migrate to async (`ExecuteCommand` → `ExecuteCommandAsync`, returning `Task<int>`). A search across the repo found ~20 call sites; every one migrates. PR1 introduces the async signature with the synchronous-wait-on-a-Task body and a net48 polyfill where `WaitForExitAsync` is unavailable; PR2 swaps the body to the true async wait.

Production code:
- `source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs` — the method itself. Async signature, two-token (`cancel`, `abandon`).
- `source/Octopus.Tentacle/Util/ISilentProcessRunner.cs` — interface and in-process wrapper become async.
- `source/Octopus.Tentacle/Util/CommandLineRunner.cs` — caller migration.
- `source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs` — `RunScript` → `RunScriptAsync`; ctor takes `abandonToken` alongside `runningScriptToken`; `Execute()` awaits the new path.
- `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs` — `LaunchShell` passes `abandonToken` from the wrapper. `RunningScriptWrapper` gains `abandonTokenSource`. `AbandonScriptAsync` calls `Abandon()` (the best-effort kill lives in the runner's abandon branch — see above).
- `source/Octopus.Tentacle.Contracts/ScriptServiceV2/` — new `AbandonScriptCommandV2.cs`, interface method on `IScriptServiceV2.cs` (per Section 1).
- `source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs` — add `AbandonedExitCode = -48`.
- Capabilities advertisement (`AbandonScriptV2`).

Immediate sync callers migrated with `.GetAwaiter().GetResult()` and a sync-boundary comment ("We're on a plain thread-pool worker — when the async work finishes it can resume on any free thread, so the block resolves normally"):
- `PowerShellPrerequisite.Check()`, `KubernetesDirectoryInformationProvider.GetDriveBytesUsingDu()`, `SystemCtlHelper.RunServiceCommand()` (×2), `LinuxServiceConfigurator` (×3), `WindowsServiceConfigurator.Sc()`, `CommandLineRunner.Execute(CommandLineInvocation, ...)`.

Kubernetes integration test scaffolding (all caller-migration, no logic change):
- `KubeCtlTool.cs`, `DockerImageLoader.cs` (×2), `KubernetesAgentInstaller.cs` (×3), `KubernetesClusterInstaller.cs` (×4), `HelmDownloader.cs`, `ToolDownloader.cs` (all under `source/Octopus.Tentacle.Kubernetes.Tests.Integration/`).

Tentacle integration test scaffolding (caller migration):
- `PowerShellStartupDetectionTests.cs` (×3), `Util/SilentProcessRunnerFixture.cs`, `Support/TentacleFetchers/LinuxTentacleFetcher.cs` (all under `source/Octopus.Tentacle.Tests.Integration/`).

**What happens to stdout/stderr after abandon.** Returning `AbandonedExitCode` unwinds the method. The outer `using (var process = new Process())` disposes the Process, which closes our end of the redirected pipes. The OS process may get EPIPE on its next stdout/stderr write. The script's runtime keeps doing whatever it's doing; many scripts ignore broken-pipe errors, and scripts that fail on them already had nowhere to log anyway. The alternative — leaving the Process and its pipes pinned in memory indefinitely — is a resource-accumulation problem.

**Async correctness watch-outs for the implementation plan:**
- Every new async method gets `.ConfigureAwait(false)`.
- No `.Result` / `.Wait()` on the new path; surface any caller that can't easily go async rather than block-on-async.
- Verify no deadlock under the Tentacle's synchronisation context (none expected, but worth confirming).

**Rejected alternatives** (documented for the reviewer's benefit):
- **Orphan the Task + release mutex via external Dispose.** Releases the mutex but leaks a threadpool worker per abandon. Tentacle eventually starves the threadpool.
- **Manual `Thread` instead of `Task`.** Same leak problem, just trades threadpool for kernel thread handles + stack memory.
- **`Thread.Abort` / `Thread.Interrupt` / `TerminateThread` P/Invoke.** No safe managed mechanism to release a thread parked in unmanaged code; can corrupt Tentacle's own state.
- **Out-of-process script worker.** Cleanly isolates the stuck-process problem but is a massive refactor far outside EFT-3295's scope. Worth a separate proposal someday.

## Section 3 — Client parameter (PR1)

Add one optional parameter, threaded through the client orchestration:

```csharp
TimeSpan? abandonAfterCancellationPendingFor = null
```

It is added to `TentacleClient.ExecuteScript`, `ITentacleClient.ExecuteScript`, and `ObservingScriptOrchestrator`.

The orchestrator's poll loop (`ObservingScriptOrchestrator.ObserveUntilComplete`) already calls `CancelScript` every iteration while the `CancellationToken` is cancelled. When the parameter is set:
1. Record when cancellation first fired.
2. Once elapsed crosses the threshold AND the `AbandonScriptV2` capability is advertised, call `AbandonScript` instead of `CancelScript`.
3. If the capability is absent, keep cancelling (skip the abandon attempt cleanly).

Add an `AbandonScript` method to the V2 script executor (`ScriptServiceV2Executor`) with the capability check. `ScriptExecutionResult.ExitCode` is the source of truth (-48 = abandoned).

**No shared constant in `Octopus.Tentacle.Contracts`.** An earlier ask for a shared abandoned-message constant was retracted; the Tentacle's own script-log line is the only customer-facing message.

## Section 4 — State, exit code, log wording

- **Exit code:** `ScriptExitCodes.AbandonedExitCode = -48`. Distinct from `CanceledExitCode (-43)`. Server-side telemetry can tell abandoned from cancelled even though task UI surfaces both as "Cancelled" per ADR-042.
- **State on GetStatus after abandon:** `(ProcessState.Complete, AbandonedExitCode, latestLogs)`. Same shape as Cancel returns today.
- **Honest log line:** `"Tentacle has abandoned this script. The underlying script process may still be running on this host."` Written once, into the workspace script log, near the end of the abandon path. Still accurate after the ADR-42 reversal: the process survives only when the best-effort kill couldn't land.
- **Workspace cleanup on subsequent `CompleteScript`:** targeted best-effort. `CompleteScript` reads the stateStore and checks the persisted exit code. If `AbandonedExitCode`, wrap `workspace.Delete` in try/catch, log a `Warn` to systemLog naming the leaked directory, return success. For any other exit code, `workspace.Delete` is called as today and exceptions propagate. The relaxed-deletion policy applies only to the rare abandon case; bugs that leak handles on normal-completion paths can't hide under a blanket try/catch. No janitor — OS-level state on the host is the customer's problem per the ticket.
- **Idempotency — actual-status return (NOT silent no-op):**
  - Abandon called twice on the same already-abandoned ticket → returns the cached `(Complete, AbandonedExitCode, logs)` response.
  - Abandon called on a ticket that completed naturally before the abandon arrived (race case the server-side session flagged) → returns `(Complete, realExitCode, logs)` with the **real exit code**, distinct from `AbandonedExitCode`. The server uses this distinction to log *"Script had already completed before abandon was needed"* instead of *"Tentacle abandoned the script"*.
  - Abandon called on an unknown ticket (never started, or already cleaned up via `CompleteScript`) → returns `(Complete, UnknownScriptExitCode, [])`, matching Cancel's behaviour for the same case.
- **Race with natural completion:** the wrapper's existing `StartScriptMutex` (or a new dedicated lock) serialises abandon entry. If state is already Complete, abandon returns the cached status per the rules above.

## Section 5 — Automated test strategy

### 5.1 `SilentProcessRunner` unit tests

Style: matches existing `SilentProcessRunnerFixture.cs`. Use short-lived helper scripts/exes as process subjects.

| Test | Trigger | Verify |
|---|---|---|
| Normal exit | Run a process that exits 0 | Returns 0; no abandon log line captured by the `info` callback spy. |
| Cancel kills process | Long-running process; fire cancel token | Within 1s: process is killed (`process.HasExited == true`). PR1: return is the kill-induced exit code (Linux 137; Windows process-defined) via the `Close`-released pipe path. PR2: return is the real kill exit code. No abandon log line. |
| Abandon while running | Long-running process; fire abandon token | Within ~100ms: returns `AbandonedExitCode`, `info` callback received exactly one call containing "Tentacle has abandoned this script". Assert `process.HasExited == false`; clean up by killing externally. |
| Abandon AFTER natural exit (race) | Process that exits in ~50ms; fire abandon token at the moment exit fires | Return value is the process's real exit code, not `AbandonedExitCode`. No abandon log line. Verifies the `abandon.IsCancellationRequested && !process.HasExited` guard. |
| Both tokens fire | Long-running process; fire cancel; with `cancel.Register` no-op'd to simulate un-killable, fire abandon | `info` callback gets the abandon log line; return value is `AbandonedExitCode`. Verifies deterministic -48 when both tokens are set. |

**Grandchild (re-parented) tests — differ by PR.** A child process spawns a grandchild that inherits stdout/stderr and outlives the child.
- **PR1:** "cancel alone does not hang" — `cancel.Register`'s `process.Close()` releases the inherited pipe handles, so the synchronous wait returns even though the grandchild holds them open. No abandon needed.
- **PR2:** "cancel then abandon" — `process.Close()` is gone, the async wait depends on the `Exited` event, so cancel alone leaves the wait pending; abandon is required to complete it and return -48.

**Async-specific timing assertion (PR2):** `WaitForExitAsync(token)` returns within ~50ms of cancellation. Wrap the await in `Stopwatch.StartNew()`; assert elapsed < 100ms. Proves the async wait is independent of process exit.

**Thread-leak regression test (PR2):** start 50 stuck processes via `ExecuteCommandAsync` (all `await`ed in parallel), fire abandon on all; capture `Process.GetCurrentProcess().Threads.Count` before and 1s after; assert delta ≤ 5 (allow for threadpool jitter). The async path should produce zero parked threads at steady state. (Under PR1 the synchronous-wait-on-a-Task body still parks a worker per running script, so this assertion is PR2-only.)

### 5.2 `ScriptServiceV2` service-layer tests

Style: matches existing service-layer fixtures using in-memory script shells and stub workspace factories.

| Test | Trigger | Verify |
|---|---|---|
| **Mutex release (load-bearing)** | Start `FullIsolation` script; abandon it; immediately start second `FullIsolation` script | Second `StartScript` returns with `State == Running` within 1s. Reading `ScriptIsolationMutex.TaskLock.Report()` between abandon and second-start shows the lock free in that window. |
| Abandon before StartScript | Call AbandonScript with a ticket never seen | Returns `(Complete, UnknownScriptExitCode)`. Matches existing Cancel behaviour for unknown ticket. |
| Abandon after CompleteScript | Start → Complete → Abandon | Returns `(Complete, UnknownScriptExitCode)` (wrapper already removed; stateStore gone). |
| Abandon then Cancel | Abandon, then Cancel same ticket | Cancel returns the cached abandoned response unchanged. Asserts via response equality. |
| **Cancel then Abandon (real flow)** | Long-running script; cancel; `cancel.Register` no-op'd to simulate un-killable; abandon | Final GetStatus returns `(Complete, AbandonedExitCode, logs)`. Log content includes the honest line. Subsequent same-ticket StartScript returns the cached state. |
| **Abandon best-effort-kills (anti-abuse)** | Long-running but killable script; call AbandonScript directly with no prior cancel | The process is killed (the runner's abandon branch ran `DoOurBestToCleanUp → Hitman.Kill`). Final state is `(Complete, AbandonedExitCode, logs)`. Confirms a direct abandon does not leave a killable process running unmanaged. |
| Abandon during StartScript launch | Concurrent: StartScript holding `StartScriptMutex`, AbandonScript called | Abandon serialises behind StartScript via the existing wrapper mutex. Final state is consistent (no half-abandoned wrapper). |
| Capability advertisement | Tentacle build with the abandon feature; query `CapabilitiesServiceV2.GetCapabilities()` | Response includes `AbandonScriptV2`. Builds without the feature do not advertise it. |

### 5.3 Client orchestrator tests

| Test | Trigger | Verify |
|---|---|---|
| Param unset → cancel only | `abandonAfterCancellationPendingFor = null`; cancel the token mid-execution | Orchestrator calls `CancelScript` each poll; never calls `AbandonScript`. |
| Threshold crossed + capability present | Param set to a short span; capability advertised; cancellation pending past the span | Orchestrator switches from `CancelScript` to `AbandonScript` after the threshold. `RecordMethodUsages` shows the `AbandonScript` call. |
| Threshold crossed + capability absent | Param set; `AbandonScriptV2` NOT advertised; cancellation pending past the span | Orchestrator keeps calling `CancelScript`; never calls `AbandonScript`; no error. |

### 5.4 Integration tests (real shells, real processes)

Style: matches `Octopus.Tentacle.Tests.Integration/ClientScriptExecutionIsolationMutex.cs` (real Tentacle, real script, mutex semantics under test).

**Timing flakiness: use the existing builders, not raw shell + `Thread.Sleep`.**
- `ScriptBuilder` (`Octopus.Tentacle.CommonTestUtils/Builders/ScriptBuilder.cs`): `.CreateFile(path)` signals "script reached this line"; `.WaitForFileToExist(path)` blocks the script on an event, not a sleep race.
- `TestExecuteShellScriptCommandBuilder` (`Octopus.Tentacle.Tests.Integration/Util/Builders/`): `.SetScriptBody(ScriptBuilder)`, `.WithIsolationLevel(...)`, `.WithIsolationMutexName(...)`, `.Build()`.
- `TentacleConfigurationTestCase.CreateBuilder()` and `ClientAndTentacleBuilder` set up real Tentacle + Halibut.
- `TentacleServiceDecoratorBuilder.RecordMethodUsages(...)` decorates the script service so the test can assert call counts for the new `AbandonScript` verb and capability negotiation.
- `Wait.For(condition, timeout, onFail, ct)` is the event-driven polling helper. Always preferred over `Task.Delay`.

**Pattern to follow:** mirror `ClientScriptExecutionIsolationMutex.cs`. Stuck-script tests use `ScriptBuilder.WaitForFileToExist(...)` as the "kernel-blocked" simulant rather than `sleep 600`. For the un-killable variant, combine the file-wait with the `Tentacle.Debug.DisableProcessKill` flag so `Hitman` becomes a no-op for the test's duration.

| Test | Trigger | Verify |
|---|---|---|
| PowerShell + cancel (kill works) | Real PowerShell, `Start-Sleep -Seconds 600`, fire Cancel, normal kill path | Final response is `(Complete, CanceledExitCode)` via the existing path. **Negative check:** abandon log line NOT present. Confirms Cancel isn't regressed into the abandon path. |
| PowerShell + abandon (kill mocked off) | Real PowerShell, sleep; `Hitman` mocked to no-op; fire Cancel; wait; fire AbandonScript | Within 2s of abandon: response is `(Complete, AbandonedExitCode, [...honest log line...])`; mutex is free (start a second `FullIsolation` script that acquires within 1s); the real PowerShell process is still alive (verified via `Process.GetProcessById`). Test cleanup: kill the leftover PowerShell. |
| **Multi-level-deep hang (ticket-mandated)** | bootstrap → Calamari-shim → user script, with `Hitman` no-op flag set | All verifications from the previous row pass through the multi-level launch chain. Confirms abandon works when the stuck process is not the immediate child of Tentacle. |
| **Re-parented grandchild — PR1** | Child spawns a grandchild that inherits stdout/stderr and outlives it; fire Cancel | Cancel alone returns (does NOT hang): `process.Close()` releases the inherited pipes. No abandon needed. |
| **Re-parented grandchild — PR2** | Same setup; fire Cancel, then AbandonScript | Cancel alone leaves the wait pending; abandon completes it and returns `(Complete, AbandonedExitCode)`. The two grandchild tests flip from PR1's "cancel alone does not hang" to PR2's "cancel then abandon". |
| Windows workspace cleanup with open handles | Run the abandon path; leave the simulated zombie holding the workspace log file open; call CompleteScript | CompleteScript returns without exception. Tentacle systemLog contains a `Warn` naming the leaked workspace directory. Workspace dir on disk still exists (assert via `Directory.Exists`). No exception bubbles up. |
| Polling Tentacle variant | Configure test fixture as Polling | All verifications from the kill-mocked-off row pass against a Polling Tentacle. |

**End-to-end async thread audit (PR2).** Capture `Process.GetCurrentProcess().Threads.Count` 5s into a stuck-script scenario; assert no thread parked attributable to the script pipeline. Most reliable proxy: total thread count not higher than baseline + epsilon.

**Normal-path timing regression check.** Run a 100-iteration benchmark of normal short-script execution (`Write-Host "x"`); compare median wall-clock time vs. a baseline build. **Verify:** median delta within margin of error.

## Section 6 — Manual testing plan

Manual scenarios on a real test Tentacle. All scenarios assume the parallel server-side build is deployed.

### Setup

- Test Octopus Server with EFT-3295 server-side build.
- Windows Tentacle (primary) + Linux Tentacle (smoke).
- Debug Tentacle build with `Tentacle.Debug.DisableProcessKill=true` making `Hitman.TryKillProcessAndChildrenRecursively` a no-op — simulant for "kill doesn't work" without engineering real kernel-level waits.
- Server-side feature flag `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` (default ON, configured on the test Octopus Server).

### Where to find things (reference for verification steps below)

- **Tentacle systemLog (Windows):** `C:\Octopus\Logs\OctopusTentacle.txt` (confirm via `Tentacle show-configuration`).
- **Tentacle systemLog (Linux):** `/etc/octopus/<instance>/Logs/OctopusTentacle.txt`.
- **Tentacle workspace root:** `<Tentacle.Home>/Work/`. Each script gets a subdirectory named after its `ScriptTicket`. Inside: `bootstrapRunner.log`, `Output.log`, `script.ps1`/`Bootstrap.sh`, the state store file.
- **Script log in UI:** Octopus Server → the task → expand the deployment step. This is what the customer sees and what gets the honest abandon line.
- **Thread count (Windows):** PowerShell `(Get-Process Tentacle).Threads.Count`, or Process Explorer's Threads tab.
- **Thread count (Linux):** `ps -o nlwp= -p $(pgrep -f Tentacle)`.
- **Capability advertisement:** Tentacle systemLog at startup contains `Negotiated capabilities: [...]`. Or enable Halibut verbose tracing server-side and inspect the `CapabilitiesResponseV2` payload.
- **Mutex state in Tentacle log:** grep for `acquiring isolation mutex` / `Lock acquired` / `Releasing lock` with the relevant task ID.

### M1 — Regression smoke (flag ON, normal script)

Deploy `Write-Host "hello"; Start-Sleep 5; Write-Host "done"`.

**Verify (all must pass):**
1. Octopus UI task status → **Success** (green tick).
2. Script log in UI shows `hello` and `done`; no abandon line.
3. Tentacle systemLog: `grep "abandon" OctopusTentacle.txt` → zero matches for this task ID.
4. Tentacle systemLog shows the normal acquire/release pair for this task ID.
5. Thread count (sampled 5s after task completes) → within ±2 of pre-test baseline.

### M2 — Cancel still works (flag ON, killable script)

`DisableProcessKill=false`. Deploy `Start-Sleep -Seconds 300`. Wait ~10s. Click **Cancel** in Octopus UI.

**Verify:**
1. UI task status transitions to **Cancelled** within 30s.
2. Tentacle systemLog shows the kill attempt followed by mutex release for this task ID.
3. PowerShell process is gone (match by PID captured from Tentacle log at script start).
4. `grep "abandon" OctopusTentacle.txt` → zero matches for this task ID. Cancel path was used, not abandon.
5. Deploy a second project to the same Tentacle → starts immediately (mutex released by the normal Cancel path).

### M3 — The Philips scenario (flag ON, unkillable script)

`Tentacle.Debug.DisableProcessKill=true`. Restart Tentacle. Capture thread-count baseline. Deploy `Start-Sleep -Seconds 600`. Note the PowerShell PID from the Tentacle log. Click **Cancel** after ~10s. Wait for the server-side abandon timeout (2 min for first release).

**Verify (all must pass; this is the load-bearing scenario):**

1. **Server side called Abandon.** Server log shows an `AbandonScript` call for this task's ticket, timestamped after the Cancel attempt + the abandon timeout.
2. **Honest log line in the customer-visible task log.** Confirm `Tentacle has abandoned this script. The underlying script process may still be running on this host.` is present in the script log section.
3. **Tentacle systemLog records the abandon path.** Shows: AbandonScript invocation received, best-effort kill attempted (no-op'd here), abandon signal fired, mutex released, wrapper removed.
4. **Mutex released — load-bearing check.** Immediately deploy a second trivial project. **Pass:** starts within 5s. **Fail:** queues with "Waiting for the script in task...".
5. **Task UI status = Cancelled** (no separate "Abandoned" state — per ADR-042).
6. **Thread count returned to baseline** (PR2). Sample 10s after the abandon. **Pass:** within ±2 of baseline. (Under PR1 a worker is parked per running script in the normal case, so use the PR2 build for this check.)
7. **The PowerShell process is still alive on the host.** `Get-Process -Id <PID>` returns the process — because the best-effort kill was no-op'd. Kill it manually at end of test for cleanup. (With `DisableProcessKill=false` abandon would have killed it; this scenario specifically simulates the kill not landing.)
8. **Exit code in the task log = -48 (AbandonedExitCode)**, distinct from `-43` (CanceledExitCode).

### M4 — Repeated abandon (thread/handle-leak check under repetition)

Capture baseline thread count and Tentacle working-set memory. Run M3 ten times back-to-back (script the loop).

**Verify:**
1. Sample thread count after each iteration (PR2 build). **Pass:** within ±5 of baseline across all ten runs. **Fail:** monotonic growth.
2. Sample working-set memory after each iteration. **Pass:** within ~50MB of baseline. **Fail:** grows by more than ~10MB per iteration.
3. After all ten runs, deploy a normal project. **Pass:** runs normally.
4. Kill leftover `powershell.exe` / `sleep` processes manually at end of test.

### M5 — Server-side flag off (Tentacle behaves as today)

Set `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` to OFF in the test Octopus Server. Restart Server. Leave Tentacle untouched.

**Verify:**
1. **Server doesn't dispatch Abandon.** Repeat the M3 setup. Wait past the 2-minute point. Server log: zero `AbandonScript` matches for this task ID.
2. **Tentacle still advertises the capability.** `CapabilitiesResponseV2` still contains `AbandonScriptV2`. The flag lives on the Server, not on Tentacle.
3. **Tentacle stays wedged.** Subsequent deployment queues with "Waiting for the script in task...". Confirms today's behaviour is preserved when Server has the feature off.
4. Recovery: restart Tentacle (the existing workaround).

### M6 — Workspace cleanup with open handles (Windows-specific)

Run M3 to completion. Note the `ScriptTicket` from the Tentacle log.

**Verify:**
1. **Workspace dir still exists.** Listing shows log files present; open handles prevent deletion.
2. **systemLog records the failure.** A `Warn`-level entry names the directory that could not be deleted, with the underlying I/O exception message.
3. **No propagated exception to Server.** `CompleteScript` returns normally; Server log shows successful completion.
4. **Tentacle continues to function.** Deploy a third project (not to the wedged workspace). **Pass:** runs normally.
5. **Manual cleanup works after the zombie process is killed.** Confirms the leak is bounded.

### M7 — Polling Tentacle variant

Register a Polling Tentacle. Repeat M3 setup and execution.

**Verify:**
1. All M3 verification points pass with no Polling-specific differences. The Halibut RPC path is the same — only connection initiation direction differs.
2. **Polling-specific check:** during the abandon, Tentacle's polling loop continues. **Pass:** polling not blocked by the abandon flow.
3. After abandon, the Polling Tentacle picks up the next deployment. **Pass:** new deployment dispatched and runs (mutex released).

### M8 — Linux smoke

On a Linux Tentacle, deploy a Bash script: `sleep 600`. Repeat M2 (kill works) and M3 (kill mocked off).

**Verify:**
1. **M2 on Linux:** `ps -p <PID>` shows the bash/sleep process gone after Cancel. Same outcomes as Windows M2.
2. **M3 on Linux:** all M3 verification points pass. Thread count via `ps -o nlwp= -p $(pgrep -f Tentacle)`. Workspace location: `/etc/octopus/<instance>/Work/<TICKET_ID>/`.
3. **Linux file-handle behaviour differs:** Linux generally allows deletion of files held open by other processes. For M6's analogue on Linux, workspace deletion is more likely to succeed even with the zombie running. Note in test result.
4. Confirms the implementation isn't accidentally Windows-only.

### M9 — Server escalation ordering

Server escalation is hardcoded at **2 minutes** post-Cancel for the first release. Ask the server-side session for a debug-build override constant to run this faster.

**Verify the killable case (no escalation expected):**
1. Run M2 (killable + cancel). Wait at least 3 minutes.
2. Server log: zero `AbandonScript` matches for this task ID. Cancel succeeded inside the 2-minute window.
3. Tentacle log: zero abandon entries for this task ID.

**Verify the unkillable case (escalation expected):**
4. Run M3 (kill mocked off + cancel). Wait through the 2-minute timeout.
5. Server log: exactly one `AbandonScript` match, timestamped ~2 minutes after the Cancel.
6. Tentacle log: one abandon entry for this task ID.

**Verify the actual-status race case** (server-side session's idempotency concern):
7. Set up M3, but let the script complete naturally just before the 2-minute timer fires (~110-second script).
8. Server fires AbandonScript anyway because the completion event hasn't reached it yet.
9. Tentacle returns `(Complete, realExitCode, logs)` — NOT `AbandonedExitCode`.
10. Server task log entry: *Script had already completed before abandon was needed.*

**Bug indicators to flag back to the server session:**
- Server calls AbandonScript on every Cancel (even killable cases) → server's escalation predicate is wrong.
- Server retries AbandonScript multiple times for the same ticket → server idempotency broken.
- Server calls AbandonScript before the 2-minute window → timer is wrong.
- Server calls AbandonScript with the Tentacle capability missing → capability gating broken.

### Sign-off criteria

To turn the feature flag on by default in a future release: M1–M5 pass on Windows; M3 + M4 pass on Linux; M7 passes on Polling; M9 confirms server escalation policy; M6 confirms workspace leak is bounded and logged.

## ADR-42 / "not killed" reversal

The original ticket and ADR-42 said the runaway process is **not** killed. This design **reverses** that: abandon now best-effort-kills the process (anti-abuse — see Abandon semantics). The Tentacle script-log line remains accurate because the process survives only when the kill genuinely couldn't land.

Follow-ups required outside this Tentacle work:
- **ADR-42** — update to reflect best-effort-kill-on-abandon.
- **EFT-3295 ticket scope** — update the "do not kill" statement.
- **Docs PR #3175** — update the customer-facing wording to match best-effort-kill-on-abandon.

**Unchanged:** the server-side RPC contract — `AbandonScript(AbandonScriptCommandV2) → ScriptStatusResponseV2`, exit code -48 — is identical to what was already agreed.

## Risks and rollout

- **Feature flag off by default** for the first release. Customer-by-customer opt-in.
- **PR2 sequencing:** PR2 (#1244) must ship only after every server in the fleet supports the abandon escalation, because PR2 removes `process.Close()` and makes server abandon the only recovery for the re-parented-grandchild case.
- **Sequence:** after EFT V1 cleanup closes (target end May 2026), before Task Cap 320, targeting Philips' July self-host release.
- **Telemetry:** count of AbandonScript calls per Tentacle per day. Spike = signal that either Cancel is broken or this feature is masking a different bug.
- **Soak test pre-release:** 1000 normal scripts with the server-side flag ON, verify no resource leak vs. flag OFF baseline.

## Open questions for external reviewer

(None remaining. Workspace cleanup policy resolved 2026-05-21 — targeted best-effort gated on `AbandonedExitCode` in the stateStore. No janitor; OS-level state on the host is the customer's responsibility per the ticket.)

## Coordination — locked with the server-side session (2026-05-21)

Aligned via Linear thread on EFT-3295 (commenter Jim, both sessions). Items below are locked unless explicitly noted.

**Contract (final shape):**

- `ScriptStatusResponseV2 AbandonScript(AbandonScriptCommandV2 command)` on `IScriptServiceV2`.
- `AbandonScriptCommandV2 { ScriptTicket Ticket; long LastLogSequence; }` — same shape as `CancelScriptCommandV2`. Server-side dropped its initial `ServerTaskId` and "cancellation correlation id" proposal; `ScriptTicket` is sufficient.
- Capability name: `AbandonScriptV2`.

**Idempotency (final):** Tentacle returns actual current status. Already-completed script returns `(Complete, realExitCode, logs)` — distinct from `AbandonedExitCode`, so the server's task log entry can record that the abandon was unnecessary. Unknown/already-cleaned-up ticket returns `(Complete, UnknownScriptExitCode, [])`, matching Cancel's existing shape.

**Capability check is the primary gate.** Server uses `BackwardsCompatibleAsyncCapabilitiesV2Decorator` to query `AbandonScriptV2` once per session. Capability absent → server does not schedule the abandon dispatch at all. The RPC-fail-then-log path stays as a defensive fallback for capability-cache staleness.

**One off-switch, server-side:** `AbandonTentacleScriptOnCancellationTimeoutFeatureToggle` (default ON). Governs whether server escalates to AbandonScript at all. No Tentacle-side flag — Tentacle's capability advertisement is binary on build version.

**Escalation timing (locked for first release):** 2 minutes. Both V1 and V2 execution pipelines escalate to AbandonScript on their next status-poll once cancellation has been pending that long. Hardcoded on the server toggle class, not configurable. Server-side trigger is a polling-loop check, not a delayed NSB message; no new server-side timers. The Tentacle-side contract is unchanged either way.

**Execution-pipeline scope (server-side, 2026-05-21):** V1 *and* V2 server-side execution pipelines call AbandonScript via the same contract. Philips is V1 self-host so V1 is the urgent path. Doesn't change anything Tentacle is building.

**Post-abandon flow:**

1. Server calls `AbandonScript` → gets `ScriptStatusResponseV2`.
2. Server publishes `TentacleScriptAbandonedEvent`.
3. Existing post-cancel path proceeds (eventually calls `CompleteScript` downstream).

**Task log wording:**

- Tentacle script log (this doc's Section 4): *Tentacle has abandoned this script. The underlying script process may still be running on this host.*
- Server task log (server session's surface). Server session's working proposal:
  - On dispatch: *Cancellation hasn't taken effect on Tentacle after 2 minutes. Abandoning the script to release the script-isolation mutex.*
  - On Tentacle returning `AbandonedExitCode`: *Tentacle abandoned the script.*
  - On Tentacle returning a real exit code (abandon unnecessary): *Script had already completed before abandon was needed.*
- I pushed back on the dispatch wording — "script-isolation mutex" exposes internal terminology to the customer. Suggested rewrite: *Cancellation hasn't taken effect on Tentacle after 2 minutes. Abandoning the script so this target can accept new deployments.* Server session's call which to ship with.
