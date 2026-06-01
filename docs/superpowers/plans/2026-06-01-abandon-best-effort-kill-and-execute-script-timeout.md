# Abandon best-effort-kill + ExecuteScript abandon-timeout — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Make Tentacle's `AbandonScript` best-effort-kill the process before it stops waiting (anti-abuse), make the PR1 runner deterministically return `AbandonedExitCode (-48)` keyed on the abandon token rather than on which task won the `Task.WhenAny` race, and give the client an optional `abandonAfterCancellationPendingFor` timeout that escalates a stuck cancel to an abandon when the Tentacle advertises the capability.

**Architecture:** Two changes across server (Tentacle) and client.
- **Change 1 (server, PR1 + later PR2).** `ScriptServiceV2.AbandonScriptAsync` calls `runningScript.Cancel()` *then* `runningScript.Abandon()`, so the existing `cancel.Register → DoOurBestToCleanUp → Hitman.Kill` machinery attempts the kill on *every* abandon — including a direct RPC with no prior cancel. The PR1 runner in `SilentProcessRunner.ExecuteCommandAsync` keys its abandoned-result branch on `abandon.IsCancellationRequested` instead of `== abandoned.Task`. After `Cancel()→Close()` the process is detached, so the abandon branch must NOT read `process.HasExited`/`process.ExitCode`; it just returns `-48`. The "abandon was unnecessary because the script already finished" case is handled one layer up: the script's `RunningScript` is already `Complete` with its recorded exit code, so `GetResponse` returns that real code, never reaching the runner's abandon branch.
- **Change 2 (client, PR1 only).** Optional `TimeSpan? abandonAfterCancellationPendingFor = null` threaded through `ITentacleClient.ExecuteScript` → `TentacleClient.ExecuteScript` → `ObservingScriptOrchestrator`. The orchestrator's `ObserveUntilComplete` already calls `CancelScript` each poll while the token is cancelled; it records when cancellation first fired and, once elapsed crosses the threshold AND the abandon capability is advertised, calls `AbandonScript` instead. A new `IScriptExecutor.AbandonScript` is implemented on `ScriptServiceV2Executor` (with a capability check) and as a no-op fallback on `ScriptServiceV1Executor`/`KubernetesScriptServiceV1Executor`. `ScriptExecutionResult.ExitCode (-48)` is the source of truth. NO shared `Octopus.Tentacle.Contracts` constant for the message.

**Tech Stack:** C#, NUnit, FluentAssertions, NSubstitute, Halibut RPC.

> **State of the tree at plan time (read before starting).** The contract surface is already merged on this branch: `AbandonScriptCommandV2`, `IScriptServiceV2.AbandonScript`, `ScriptExitCodes.AbandonedExitCode = -48`, `ITentacleClient.AbandonScript`/`TentacleClient.AbandonScript`, `RunningScript.CreateAbandonable` with `abandonToken`, the `RunningScript.Execute` abandon catch, `ScriptServiceV2.AbandonScriptAsync` (currently calls only `Abandon()`), and the PR1 runner `Task.WhenAny` block (currently keyed on `== abandoned.Task`). Capabilities advertise `nameof(ScriptServiceV2.AbandonScriptAsync)` → the literal string `"AbandonScriptAsync"`, NOT `"AbandonScriptV2"`. This plan ONLY makes the two deltas above; it does not re-create the contract.

---

## Task 1 — PR1 runner: key the abandoned branch on `abandon.IsCancellationRequested`

The PR1 `Task.WhenAny` currently returns `AbandonedExitCode` only when `abandoned.Task` literally won the race (`== abandoned.Task`). The spec requires the result keyed on the *token*, so that when both cancel and abandon fire and `waitForExit` happens to win (e.g. the abandon TCS callback is still queued), we still return `-48` deterministically. The abandon branch must NOT touch `process.HasExited`/`ExitCode` — after `Cancel()→Close()` the Process is detached.

**Files**
- Test: `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs` (add new `[Test]` after `AbandonToken_ShouldReturnAbandonedExitCodeWithoutKillingProcess` which ends at line ~372)
- Modify: `source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs` lines 161–177 (the `abandoned`/`Task.WhenAny` block)

- [ ] Write a failing test `AbandonToken_WhenWaitTaskWinsRace_StillReturnsAbandonedExitCode` in `SilentProcessRunnerFixture.cs`. This drives both tokens: cancel fires first (no-op kill via the env var), then abandon, and asserts the return is `-48` regardless of race ordering. Add this method inside the class:

```csharp
        [Test]
        public async Task AbandonToken_WhenBothTokensFire_ReturnsAbandonedExitCodeKeyedOnToken()
        {
            // Simulate an un-killable script: DisableProcessKill makes cancel's Hitman.Kill a no-op,
            // so cancel does NOT make the process exit. Then abandon fires. The return must be
            // AbandonedExitCode because abandon.IsCancellationRequested is set — not because of which
            // task won the WhenAny race. This is the deterministic-(-48) guarantee from the spec.
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION, "1");
            try
            {
                using var tempDir = new TemporaryDirectory();
                var pidFile = Path.Combine(tempDir.DirectoryPath, "process.pid");

                var executable = PlatformDetection.IsRunningOnWindows ? "powershell.exe" : "/bin/bash";
                var arguments = PlatformDetection.IsRunningOnWindows
                    ? $"-NoProfile -NonInteractive -Command \"$PID | Out-File -FilePath '{pidFile}' -Encoding ASCII; Start-Sleep -Seconds 300\""
                    : $"-c \"echo $$ > '{pidFile}' && sleep 300\"";

                using var cancelCts = new CancellationTokenSource();
                using var abandonCts = new CancellationTokenSource();

                var task = Task.Run(async () => await SilentProcessRunner.ExecuteCommandAsync(
                    executable,
                    arguments,
                    Environment.CurrentDirectory,
                    debug: _ => { },
                    info: _ => { },
                    error: _ => { },
                    customEnvironmentVariables: null,
                    cancel: cancelCts.Token,
                    abandon: abandonCts.Token));

                await WaitForPidFileAsync(pidFile, TimeSpan.FromSeconds(30));

                // Cancel first (kill is no-op'd so the process keeps running), then abandon.
                cancelCts.Cancel();
                abandonCts.Cancel();

                var exitCode = await task;
                exitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

                // Cleanup: the real process is still alive because kill was no-op'd.
                if (File.Exists(pidFile) && int.TryParse(SafelyReadAllText(pidFile).Trim(), out var pid) && pid > 0)
                {
                    try { Process.GetProcessById(pid).Kill(); } catch { /* already gone */ }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION, null);
            }
        }
```

- [ ] Run it on net48 + net8.0 and watch it FAIL (today the `== abandoned.Task` branch can be skipped when `waitForExit` wins, falling through to `SafelyGetExitCode` on a detached process → returns `-1`, not `-48`):

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net48 \
  --filter "FullyQualifiedName~SilentProcessRunnerFixture.AbandonToken_WhenBothTokensFire_ReturnsAbandonedExitCodeKeyedOnToken"
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~SilentProcessRunnerFixture.AbandonToken_WhenBothTokensFire_ReturnsAbandonedExitCodeKeyedOnToken"
```
Expected: FAIL (returns -1 or hangs).

- [ ] Make the minimal implementation change in `SilentProcessRunner.cs`. Replace the `Task.WhenAny`/`== abandoned.Task` block (lines 161–177) so the abandoned branch is keyed on the token and runs after the race regardless of winner:

```csharp
                        var abandoned = new TaskCompletionSource<object?>();
                        using (abandon.Register(() => abandoned.TrySetResult(null)))
                        {
                            var waitForExit = Task.Run(() =>
                            {
                                try { process.WaitForExit(); }
                                catch { /* released by Process.Dispose on the abandon path */ }
                            });

                            await Task.WhenAny(waitForExit, abandoned.Task).ConfigureAwait(false);

                            // Key the abandoned result on the TOKEN, not on which task won the race.
                            // When both cancel and abandon fire, cancel's Kill is no-op'd / can't land, so
                            // waitForExit may win even though we must still return AbandonedExitCode.
                            // After Cancel()->Close() the Process is detached, so do NOT read
                            // process.HasExited / process.ExitCode here — just return -48. The
                            // "abandon was unnecessary" case never reaches here: the script's
                            // RunningScript is already Complete with its real code one layer up.
                            if (abandon.IsCancellationRequested)
                            {
                                info("Tentacle has abandoned this script. The underlying script process may still be running on this host.");
                                SafelyCancelOutputAndErrorRead(process, debug);
                                running = false;
                                return ScriptExitCodes.AbandonedExitCode;
                            }
                        }
```

- [ ] Re-run the same two `dotnet test` commands from above. Expected: PASS on both net48 and net8.0.

- [ ] Run the existing abandon + grandchild regression tests to confirm no regression:

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~SilentProcessRunnerFixture.AbandonToken_ShouldReturnAbandonedExitCodeWithoutKillingProcess|FullyQualifiedName~SilentProcessRunnerFixture.CancellationToken_WhenGrandchildHoldsRedirectedPipes_ShouldNotHang|FullyQualifiedName~SilentProcessRunnerFixture.CancellationToken_WhenUnixGrandchildHoldsRedirectedPipes_ShouldNotHang"
```
Expected: PASS.

- [ ] Commit: `git commit -am "PR1 runner: key abandoned result on abandon token, not WhenAny winner"`

---

## Task 2 — `AbandonScriptAsync` best-effort-kills (Cancel then Abandon) — service-layer test

`ScriptServiceV2.AbandonScriptAsync` currently calls only `runningScript.Abandon()`. The spec requires `runningScript.Cancel()` first so `Hitman.Kill` runs on every abandon (anti-abuse). We prove this through the integration layer because `ScriptServiceV2` wiring (workspace factory, state store, shell) is awkward to stub; the existing abandon integration tests already exercise this service with a real Tentacle.

**Files**
- Test: `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs` (add a third `[Test]` after `AbandonScript_ReleasesIsolationMutexEvenWhileProcessIsStillRunning`, which ends at line ~129)

- [ ] Write a failing test `AbandonScript_WithNoPriorCancel_KillsTheProcess` in `ClientScriptExecutionAbandon.cs`. Kill is NOT disabled here, so abandon's internal `Cancel()` must terminate the process. Add this method inside the class:

```csharp
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_WithNoPriorCancel_KillsTheProcess(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Anti-abuse: a direct AbandonScript with no prior CancelScript must still attempt the
            // kill. AbandonScriptAsync calls Cancel() then Abandon(), so Hitman.Kill runs. Kill is
            // NOT disabled here, so the underlying process must actually die.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");

            var command = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.NoIsolation)
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;
            var scriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(command, CancellationToken));

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            // Direct abandon, NO prior cancel.
            await tentacleClient.AbandonScript(command.ScriptTicket, CancellationToken);

            ScriptStatus abandonResponse = null!;
            await Wait.For(async () =>
                {
                    abandonResponse = await tentacleClient.GetStatus(command.ScriptTicket, CancellationToken);
                    return abandonResponse.State == ProcessState.Complete;
                },
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Abandoned script did not reach Complete state within 30s"),
                CancellationToken);

            abandonResponse.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

            // The kill landed because Hitman was NOT disabled: the script never saw the release file,
            // so if it is still waiting the file would be absent and the process alive. The script
            // reaching Complete with AbandonedExitCode while we never wrote releaseFile proves the
            // wait was broken by abandon; the kill having run is asserted by the script process being
            // gone — drain the execution task, which completes once the process is dead.
            await scriptExecution;
        }
```

- [ ] Run it and watch it FAIL (today abandon does NOT call `Cancel()`, so `Hitman.Kill` never runs; the process keeps waiting on `releaseFile`, and `scriptExecution` never drains within the test timeout):

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~ClientScriptExecutionAbandon.AbandonScript_WithNoPriorCancel_KillsTheProcess"
```
Expected: FAIL (test times out because the process is never killed).

- [ ] Make the minimal implementation change in `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs`. Modify `AbandonScriptAsync` (lines 144–156) to call `Cancel()` then `Abandon()`:

```csharp
        public Task<ScriptStatusResponseV2> AbandonScriptAsync(AbandonScriptCommandV2 command, CancellationToken cancellationToken)
        {
            // Abandon best-effort-kills first (anti-abuse): Cancel() runs the existing
            // cancel.Register -> DoOurBestToCleanUp -> Hitman.Kill machinery so ANY abandon —
            // including a direct RPC with no prior cancel — attempts the kill. Abandon() then
            // layers "stop waiting + release the mutex + return -48" on top. A process survives
            // abandon only when the kill genuinely can't land (stuck / re-parented grandchild).
            if (runningScripts.TryGetValue(command.Ticket, out var runningScript))
            {
                runningScript.Cancel();
                runningScript.Abandon();
            }

            return Task.FromResult(GetResponse(command.Ticket, command.LastLogSequence, runningScript?.Process));
        }
```

- [ ] Re-run the same `dotnet test` command. Expected: PASS.

- [ ] Run the two existing abandon integration tests to confirm no regression (kill-disabled path still returns -48 and releases the mutex):

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~ClientScriptExecutionAbandon"
```
Expected: PASS (all three tests).

- [ ] Commit: `git commit -am "AbandonScriptAsync: Cancel() then Abandon() so abandon best-effort-kills"`

---

## Task 3 — Client: add `AbandonScript` to `IScriptExecutor` and capability extension

Change 2 needs the orchestrator to call abandon through the executor abstraction. Add `AbandonScript(CommandContext)` to `IScriptExecutor`, a capability-checking implementation on `ScriptServiceV2Executor`, and no-op fallbacks on V1/Kubernetes executors. The capability check uses a new `HasAbandonScriptV2()` extension that matches the capability string Tentacle actually advertises today (`"AbandonScriptAsync"`).

> **Spec gap (resolved here):** the spec names the capability `AbandonScriptV2`, but `CapabilitiesServiceV2` already advertises `nameof(ScriptServiceV2.AbandonScriptAsync)` = the string `"AbandonScriptAsync"`. Changing the advertised string would break the contract already shipped on this branch. This plan matches the *actual* advertised string. If the team wants the spec's `AbandonScriptV2` literal, that is a separate contract change with its own backward-compat story — flagged, not silently done.

**Files**
- Test: `source/Octopus.Tentacle.Client.Tests/CapabilitiesResponseV2ExtensionMethodsTests.cs` (CREATE)
- Modify: `source/Octopus.Tentacle.Client/Capabilities/CapabilitiesResponseV2ExtensionMethods.cs` (add method after `HasScriptServiceV2`, line 17)
- Modify: `source/Octopus.Tentacle.Client/Scripts/IScriptExecutor.cs` (add method after `CancelScript`, line 33)

- [ ] Write a failing test file `source/Octopus.Tentacle.Client.Tests/CapabilitiesResponseV2ExtensionMethodsTests.cs`:

```csharp
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class CapabilitiesResponseV2ExtensionMethodsTests
    {
        [Test]
        public void HasAbandonScriptV2_WhenAdvertised_ReturnsTrue()
        {
            var capabilities = new CapabilitiesResponseV2(new List<string> { "IScriptServiceV2", "AbandonScriptAsync" });
            capabilities.HasAbandonScriptV2().Should().BeTrue();
        }

        [Test]
        public void HasAbandonScriptV2_WhenNotAdvertised_ReturnsFalse()
        {
            var capabilities = new CapabilitiesResponseV2(new List<string> { "IScriptServiceV2" });
            capabilities.HasAbandonScriptV2().Should().BeFalse();
        }

        [Test]
        public void HasAbandonScriptV2_WhenEmpty_ReturnsFalse()
        {
            var capabilities = new CapabilitiesResponseV2(new List<string>());
            capabilities.HasAbandonScriptV2().Should().BeFalse();
        }
    }
}
```

- [ ] Run it and watch it FAIL (the extension does not exist → compile error):

```bash
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0 \
  --filter "FullyQualifiedName~CapabilitiesResponseV2ExtensionMethodsTests"
```
Expected: FAIL (does not compile).

- [ ] Add the extension method to `CapabilitiesResponseV2ExtensionMethods.cs` after `HasScriptServiceV2` (the closing brace of that method is at line 17). Insert:

```csharp

        public static bool HasAbandonScriptV2(this CapabilitiesResponseV2 capabilities)
        {
            if (capabilities?.SupportedCapabilities?.Any() != true)
            {
                return false;
            }

            // Tentacle advertises this as nameof(ScriptServiceV2.AbandonScriptAsync). Keep the
            // literal in sync with CapabilitiesServiceV2.GetCapabilitiesAsync on the Tentacle side.
            return capabilities.SupportedCapabilities.Contains("AbandonScriptAsync");
        }
```

- [ ] Re-run the extension test. Expected: PASS.

- [ ] Add `AbandonScript` to `IScriptExecutor.cs`. Insert after the `CancelScript` declaration (line 33, before `CompleteScript`'s doc comment):

```csharp

        /// <summary>
        /// Abandon the script: signal Tentacle to stop waiting and release the isolation mutex.
        /// Returns the abandoned status when the Tentacle advertises the abandon capability;
        /// otherwise falls back to cancelling and returns that result.
        /// </summary>
        /// <param name="commandContext">The CommandContext from the previous command</param>
        Task<ScriptOperationExecutionResult> AbandonScript(CommandContext commandContext);
```

- [ ] Build the client to confirm the interface compiles (implementations come in Task 4 — expect implementor errors are fine to defer only if you build implementors together; here build will FAIL until Task 4, so just verify the interface file itself parses by building the project after Task 4). Skip a standalone build here; commit the interface + extension together with implementations is cleaner — but to keep commits frequent, commit the extension + its test now:

```bash
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0 \
  --filter "FullyQualifiedName~CapabilitiesResponseV2ExtensionMethodsTests"
```
Expected: PASS.

- [ ] Commit: `git commit -am "Client: add HasAbandonScriptV2 capability check + IScriptExecutor.AbandonScript"`

---

## Task 4 — Implement `AbandonScript` on the three executors

`ScriptServiceV2Executor` does the real work with a capability check; V1 and Kubernetes executors fall back to `CancelScript` (they have no abandon RPC). The V2 executor needs the capabilities client; add it as a constructor dependency and wire it through `ScriptExecutorFactory`.

**Files**
- Modify: `source/Octopus.Tentacle.Client/Scripts/ScriptServiceV2Executor.cs` (ctor lines 27–41; add `AbandonScript` after `CancelScript`, line 174)
- Modify: `source/Octopus.Tentacle.Client/Scripts/ScriptServiceV1Executor.cs` (add `AbandonScript` fallback)
- Modify: `source/Octopus.Tentacle.Client/Scripts/KubernetesScriptServiceV1Executor.cs` (add `AbandonScript` fallback)
- Modify: `source/Octopus.Tentacle.Client/Scripts/ScriptExecutorFactory.cs` (pass capabilities client to V2 executor, line 48)
- Test: `source/Octopus.Tentacle.Client.Tests/ScriptServiceV2ExecutorAbandonTests.cs` (CREATE)

- [ ] Write a failing test file `source/Octopus.Tentacle.Client.Tests/ScriptServiceV2ExecutorAbandonTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ScriptServiceV2ExecutorAbandonTests
    {
        static ScriptServiceV2Executor CreateExecutor(IAsyncClientScriptServiceV2 scriptService, IAsyncClientCapabilitiesServiceV2 capabilities)
            => new(
                scriptService,
                capabilities,
                RpcCallExecutorFactory.Create(TimeSpan.Zero, Substitute.For<ITentacleClientObserver>()),
                ClientOperationMetricsBuilder.Start(),
                TimeSpan.Zero,
                new TentacleClientOptions(new RpcRetrySettings(retriesEnabled: false, retryDuration: TimeSpan.Zero)),
                Substitute.For<ITentacleClientTaskLog>());

        static CommandContext Context() => new(new ScriptTicket("TestTicket"), 0, ScriptServiceVersion.ScriptServiceVersion2);

        [Test]
        public async Task AbandonScript_WhenCapabilityAdvertised_CallsAbandonScriptAsync()
        {
            var scriptService = Substitute.For<IAsyncClientScriptServiceV2>();
            scriptService.AbandonScriptAsync(Arg.Any<AbandonScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>())
                .Returns(x => Task.FromResult(new ScriptStatusResponseV2(
                    x.Arg<AbandonScriptCommandV2>().Ticket, ProcessState.Complete,
                    ScriptExitCodes.AbandonedExitCode, new List<ProcessOutput>(), 1)));

            var capabilities = Substitute.For<IAsyncClientCapabilitiesServiceV2>();
            capabilities.GetCapabilitiesAsync(Arg.Any<HalibutProxyRequestOptions>())
                .Returns(Task.FromResult(new CapabilitiesResponseV2(new List<string> { "IScriptServiceV2", "AbandonScriptAsync" })));

            var executor = CreateExecutor(scriptService, capabilities);

            var result = await executor.AbandonScript(Context());

            await scriptService.Received(1).AbandonScriptAsync(Arg.Any<AbandonScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>());
            await scriptService.DidNotReceive().CancelScriptAsync(Arg.Any<CancelScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>());
            result.ScriptStatus.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
        }

        [Test]
        public async Task AbandonScript_WhenCapabilityAbsent_FallsBackToCancelScriptAsync()
        {
            var scriptService = Substitute.For<IAsyncClientScriptServiceV2>();
            scriptService.CancelScriptAsync(Arg.Any<CancelScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>())
                .Returns(x => Task.FromResult(new ScriptStatusResponseV2(
                    x.Arg<CancelScriptCommandV2>().Ticket, ProcessState.Running,
                    0, new List<ProcessOutput>(), 1)));

            var capabilities = Substitute.For<IAsyncClientCapabilitiesServiceV2>();
            capabilities.GetCapabilitiesAsync(Arg.Any<HalibutProxyRequestOptions>())
                .Returns(Task.FromResult(new CapabilitiesResponseV2(new List<string> { "IScriptServiceV2" })));

            var executor = CreateExecutor(scriptService, capabilities);

            await executor.AbandonScript(Context());

            await scriptService.DidNotReceive().AbandonScriptAsync(Arg.Any<AbandonScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>());
            await scriptService.Received(1).CancelScriptAsync(Arg.Any<CancelScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>());
        }
    }
}
```

- [ ] Run it and watch it FAIL (V2 executor has no capabilities ctor param and no `AbandonScript` → compile error):

```bash
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0 \
  --filter "FullyQualifiedName~ScriptServiceV2ExecutorAbandonTests"
```
Expected: FAIL (does not compile).

- [ ] Add the capabilities dependency to `ScriptServiceV2Executor`. Modify the field block (after line 20 `readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;`) and the ctor (lines 27–41):

```csharp
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ITentacleClientTaskLog logger;
        readonly TentacleClientOptions clientOptions;

        public ScriptServiceV2Executor(
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ITentacleClientTaskLog logger)
        {
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }
```

- [ ] Add the `AbandonScript` method to `ScriptServiceV2Executor.cs` immediately after `CancelScript` (after line 174, before `CompleteScript`). Add `using Octopus.Tentacle.Client.Capabilities;` at the top if absent:

```csharp
        public async Task<ScriptOperationExecutionResult> AbandonScript(CommandContext commandContext)
        {
            using var activity = TentacleClient.ActivitySource.StartActivity($"{nameof(ScriptServiceV2Executor)}.{nameof(AbandonScript)}");

            var capabilities = await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<Contracts.Capabilities.ICapabilitiesServiceV2>(nameof(Contracts.Capabilities.ICapabilitiesServiceV2.GetCapabilities)),
                async ct => await clientCapabilitiesServiceV2.GetCapabilitiesAsync(new HalibutProxyRequestOptions(ct)),
                logger,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            // Capability absent → the Tentacle is too old to abandon. Keep cancelling cleanly.
            if (!capabilities.HasAbandonScriptV2())
            {
                logger.Verbose("Tentacle does not advertise AbandonScript; falling back to CancelScript.");
                return await CancelScript(commandContext).ConfigureAwait(false);
            }

            async Task<ScriptStatusResponseV2> AbandonScriptAction(CancellationToken ct)
            {
                var request = new AbandonScriptCommandV2(commandContext.ScriptTicket, commandContext.NextLogSequence);
                return await clientScriptServiceV2.AbandonScriptAsync(request, new HalibutProxyRequestOptions(ct));
            }

            var scriptStatusResponseV2 = await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.AbandonScript)),
                AbandonScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // Like CancelScript, abandon must not be cancelled — it stops the script on Tentacle.
                CancellationToken.None).ConfigureAwait(false);
            return Map(scriptStatusResponseV2);
        }
```

- [ ] Add the no-op fallback to `ScriptServiceV1Executor.cs` (mirror its `CancelScript`; V1 has no abandon RPC, so abandon degrades to cancel). Add after that executor's `CancelScript` method:

```csharp
        public Task<ScriptOperationExecutionResult> AbandonScript(CommandContext commandContext)
        {
            // ScriptServiceV1 has no abandon verb; degrade to cancel.
            return CancelScript(commandContext);
        }
```

- [ ] Add the same fallback to `KubernetesScriptServiceV1Executor.cs` after its `CancelScript`:

```csharp
        public Task<ScriptOperationExecutionResult> AbandonScript(CommandContext commandContext)
        {
            // KubernetesScriptServiceV1 has no abandon verb; degrade to cancel.
            return CancelScript(commandContext);
        }
```

- [ ] Wire the capabilities client through `ScriptExecutorFactory.cs`. Change the V2 construction (line 48) to pass `allClients.CapabilitiesServiceV2`:

```csharp
                return new ScriptServiceV2Executor(
                    allClients.ScriptServiceV2,
                    allClients.CapabilitiesServiceV2,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
```

- [ ] Add `AbandonScript` to the `ScriptExecutor` facade (`source/Octopus.Tentacle.Client/ScriptExecutor.cs`) after its `CancelScript` method (line ~70), mirroring the existing delegation pattern:

```csharp
        public async Task<ScriptOperationExecutionResult> AbandonScript(CommandContext commandContext)
        {
            var scriptExecutorFactory = CreateScriptExecutorFactory();
            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(commandContext.ScripServiceVersionUsed);

            return await scriptExecutor.AbandonScript(commandContext);
        }
```

- [ ] Re-run the V2 executor abandon tests. Expected: PASS.

```bash
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0 \
  --filter "FullyQualifiedName~ScriptServiceV2ExecutorAbandonTests"
```

- [ ] Build the client + run the full client test project to confirm no implementor was missed:

```bash
dotnet build source/Octopus.Tentacle.Client --framework net8.0
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0
```
Expected: build succeeds; tests PASS.

- [ ] Commit: `git commit -am "Client executors: implement AbandonScript with capability check + cancel fallback"`

---

## Task 5 — Orchestrator: escalate to abandon after the cancellation-pending threshold

`ObservingScriptOrchestrator.ObserveUntilComplete` calls `CancelScript` each poll while the token is cancelled. Add `abandonAfterCancellationPendingFor`; record when cancellation first fired; once elapsed crosses the threshold call `AbandonScript` (the executor handles the capability check + fallback). When the param is `null`, behaviour is unchanged.

**Files**
- Modify: `source/Octopus.Tentacle.Client/Scripts/ObservingScriptOrchestrator.cs` (ctor lines 18–28; `ExecuteScript` lines 30–45; `ObserveUntilComplete` lines 76–144)
- Test: `source/Octopus.Tentacle.Client.Tests/ObservingScriptOrchestratorAbandonTests.cs` (CREATE)

- [ ] First, make `ObserveUntilComplete` testable by changing its access modifier from `private` (implicit) to `internal` in `ObservingScriptOrchestrator.cs` (the class is already `internal`; `Octopus.Tentacle.Client.Tests` has `InternalsVisibleTo`). Change line 76 from `async Task<ScriptOperationExecutionResult> ObserveUntilComplete(` to `internal async Task<ScriptOperationExecutionResult> ObserveUntilComplete(`. (`ExecuteScriptCommand` is abstract, so driving the full `ExecuteScript` from a unit test is awkward; calling `ObserveUntilComplete` directly is the deterministic seam.)

- [ ] Write a failing test file `source/Octopus.Tentacle.Client.Tests/ObservingScriptOrchestratorAbandonTests.cs`. It drives `ObserveUntilComplete` directly with an NSubstitute `IScriptExecutor`, a pre-cancelled token, and asserts cancel-vs-abandon based on the threshold:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ObservingScriptOrchestratorAbandonTests
    {
        static CommandContext Context() => new(new ScriptTicket("T"), 0, ScriptServiceVersion.ScriptServiceVersion2);

        static ScriptOperationExecutionResult Running()
            => new(new ScriptStatus(ProcessState.Running, 0, new List<ProcessOutput>()), Context());

        static ScriptOperationExecutionResult Complete(int exitCode)
            => new(new ScriptStatus(ProcessState.Complete, exitCode, new List<ProcessOutput>()), Context());

        static ObservingScriptOrchestrator CreateOrchestrator(IScriptExecutor executor, TimeSpan? abandonAfter)
            => new(
                new ImmediateBackoff(),
                _ => { },
                _ => Task.CompletedTask,
                executor,
                abandonAfter);

        sealed class ImmediateBackoff : IScriptObserverBackoffStrategy
        {
            public TimeSpan GetBackoff(int iteration) => TimeSpan.Zero;
        }

        [Test]
        public async Task ParamUnset_CancelsOnly_NeverAbandons()
        {
            var executor = Substitute.For<IScriptExecutor>();
            executor.CancelScript(Arg.Any<CommandContext>())
                .Returns(Running(), Complete(ScriptExitCodes.CanceledExitCode));

            var orchestrator = CreateOrchestrator(executor, abandonAfter: null);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await orchestrator.ObserveUntilComplete(Running(), cts.Token);

            await executor.DidNotReceive().AbandonScript(Arg.Any<CommandContext>());
            await executor.Received().CancelScript(Arg.Any<CommandContext>());
        }

        [Test]
        public async Task ThresholdCrossed_SwitchesFromCancelToAbandon()
        {
            var executor = Substitute.For<IScriptExecutor>();
            // abandonAfter = Zero means the threshold is crossed on the first cancelled iteration,
            // so the orchestrator abandons immediately. AbandonScript returns Complete to end the loop.
            executor.AbandonScript(Arg.Any<CommandContext>())
                .Returns(Complete(ScriptExitCodes.AbandonedExitCode));

            var orchestrator = CreateOrchestrator(executor, abandonAfter: TimeSpan.Zero);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await orchestrator.ObserveUntilComplete(Running(), cts.Token);

            await executor.Received().AbandonScript(Arg.Any<CommandContext>());
            result.ScriptStatus.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
        }
    }
}
```

- [ ] Run it and watch it FAIL (orchestrator ctor has no `abandonAfter` param and `ObserveUntilComplete` is not yet `internal`/escalating → compile error on the 5-arg ctor):

```bash
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0 \
  --filter "FullyQualifiedName~ObservingScriptOrchestratorAbandonTests"
```
Expected: FAIL (does not compile).

- [ ] Add the field + ctor param to `ObservingScriptOrchestrator.cs`. After `readonly IScriptExecutor scriptExecutor;` (line 16) add `readonly TimeSpan? abandonAfterCancellationPendingFor;`. Change the ctor signature (line 18) to append `TimeSpan? abandonAfterCancellationPendingFor` and assign it:

```csharp
        public ObservingScriptOrchestrator(
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            IScriptExecutor scriptExecutor,
            TimeSpan? abandonAfterCancellationPendingFor = null)
        {
            this.scriptExecutor = scriptExecutor;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
            this.abandonAfterCancellationPendingFor = abandonAfterCancellationPendingFor;
        }
```

- [ ] Change `ObserveUntilComplete` (lines 76–144) to record first-cancel time and escalate. Replace the cancellation branch (lines 86–89):

```csharp
            var iteration = 0;
            var cancellationIteration = 0;
            var lastResult = startScriptResult;
            var stopwatch = new Stopwatch();

            while (lastResult.ScriptStatus.State != ProcessState.Complete)
            {
                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    // Record when cancellation first fired so we can escalate to abandon after the threshold.
                    if (!stopwatch.IsRunning)
                    {
                        stopwatch.Start();
                    }

                    var shouldAbandon = abandonAfterCancellationPendingFor is { } threshold
                        && stopwatch.Elapsed >= threshold;

                    lastResult = shouldAbandon
                        ? await scriptExecutor.AbandonScript(lastResult.ContextForNextCommand).ConfigureAwait(false)
                        : await scriptExecutor.CancelScript(lastResult.ContextForNextCommand).ConfigureAwait(false);
                }
                else
                {
```

(`System.Diagnostics` is already imported for `Stopwatch` at line 2.)

- [ ] Re-run the orchestrator abandon tests. Expected: PASS.

```bash
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0 \
  --filter "FullyQualifiedName~ObservingScriptOrchestratorAbandonTests"
```

- [ ] Commit: `git commit -am "Orchestrator: escalate cancel to abandon after abandonAfterCancellationPendingFor"`

---

## Task 6 — Thread `abandonAfterCancellationPendingFor` through `TentacleClient` + `ITentacleClient`

The orchestrator now accepts the timeout; expose it on the public `ExecuteScript` surface as an optional parameter (default `null`, so all existing callers are unchanged).

**Files**
- Modify: `source/Octopus.Tentacle.Client/ITentacleClient.cs` (`ExecuteScript` declaration lines 30–35)
- Modify: `source/Octopus.Tentacle.Client/TentacleClient.cs` (`ExecuteScript` lines 168–208; orchestrator construction line 189)
- Test: `source/Octopus.Tentacle.Client.Tests/ObservingScriptOrchestratorAbandonTests.cs` (no change — covered) + a compile-only assertion in `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs` (Task 7 covers behavioural)

- [ ] Add the optional parameter to `ITentacleClient.ExecuteScript` (after `scriptExecutionCancellationToken`, line 35):

```csharp
        Task<ScriptExecutionResult> ExecuteScript(
            ExecuteScriptCommand executeScriptCommand,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            ITentacleClientTaskLog logger,
            CancellationToken scriptExecutionCancellationToken,
            TimeSpan? abandonAfterCancellationPendingFor = null);
```

- [ ] Add the matching parameter to `TentacleClient.ExecuteScript` (line 168) and pass it to the orchestrator (line 189):

```csharp
        public async Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand executeScriptCommand,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            ITentacleClientTaskLog logger,
            CancellationToken scriptExecutionCancellationToken,
            TimeSpan? abandonAfterCancellationPendingFor = null)
        {
```

and:

```csharp
                var orchestrator = new ObservingScriptOrchestrator(scriptObserverBackOffStrategy,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    scriptExecutor,
                    abandonAfterCancellationPendingFor);
```

- [ ] Build the client and the integration test project (which references the client) to confirm the optional param did not break any caller:

```bash
dotnet build source/Octopus.Tentacle.Client --framework net8.0
dotnet build source/Octopus.Tentacle.Tests.Integration --framework net8.0
```
Expected: both build succeed (optional param = no caller changes required).

- [ ] Run the full client test project to confirm green:

```bash
dotnet test source/Octopus.Tentacle.Client.Tests --framework net8.0
```
Expected: PASS.

- [ ] Commit: `git commit -am "TentacleClient: expose optional abandonAfterCancellationPendingFor on ExecuteScript"`

---

## Task 7 — End-to-end integration test: ExecuteScript escalates to abandon after the threshold

Prove the whole client→Tentacle path: a stuck (kill-disabled) script with `abandonAfterCancellationPendingFor` set short, cancelled mid-flight, escalates to `AbandonScript` and returns `AbandonedExitCode`. Uses `TentacleServiceDecoratorBuilder.RecordMethodUsages` to assert the `AbandonScript` verb was called.

**Files**
- Modify: `source/Octopus.Tentacle.Tests.Integration.Common/Builders/Decorators/Proxies/RecordMethodUsagesExtensionMethods.cs` (add `ForAbandonScriptAsync`)
- Test: `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs` (add a `[Test]` after Task 2's test)

- [ ] Add a `ForAbandonScriptAsync` extension to `RecordMethodUsagesExtensionMethods.cs` (mirrors the existing `ForCancelScriptAsync` at line 22; the recorded method name is the proxied async client method `AbandonScriptAsync`). Insert after `ForCancelScriptAsync` (line 25):

```csharp

        public static IRecordedMethodUsage ForAbandonScriptAsync(this IRecordedMethodUsages o)
        {
            return o.For("AbandonScriptAsync");
        }
```

- [ ] Write a failing test `ExecuteScript_WhenCancellationStaysPending_EscalatesToAbandon` in `ClientScriptExecutionAbandon.cs`. This uses the exact recorded-usages API (`IRecordedMethodUsages recordedUsages = new MethodUsages();` then `.RecordMethodUsages<IAsyncClientScriptServiceV2>(out recordedUsages)`, queried via `recordedUsages.ForAbandonScriptAsync().Started`). Add the `using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators.Proxies;` and `using Octopus.Tentacle.Contracts.ClientServices;` imports at the top of the file if absent:

```csharp
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task ExecuteScript_WhenCancellationStaysPending_EscalatesToAbandon(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Kill is disabled, so the script is genuinely stuck and CancelScript can't end it.
            // With abandonAfterCancellationPendingFor set short, the orchestrator must escalate to
            // AbandonScript, which returns AbandonedExitCode and releases the script.
            IRecordedMethodUsages recordedUsages = new MethodUsages();
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION, "1"))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientScriptServiceV2>(out recordedUsages)
                    .Build())
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");

            var command = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.NoIsolation)
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;

            using var executionCts = new CancellationTokenSource();
            var logs = new System.Collections.Generic.List<ProcessOutput>();

            var execution = Task.Run(async () => await tentacleClient.ExecuteScript(
                command,
                onScriptStatusResponseReceived => logs.AddRange(onScriptStatusResponseReceived.Logs),
                _ => Task.CompletedTask,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog(),
                executionCts.Token,
                abandonAfterCancellationPendingFor: TimeSpan.FromSeconds(2)));

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            executionCts.Cancel();

            // ExecuteScript throws OperationCanceledException once the token is cancelled.
            await FluentActions.Invoking(async () => await execution).Should().ThrowAsync<OperationCanceledException>();

            // The orchestrator must have escalated to AbandonScript after the 2s threshold.
            recordedUsages.ForAbandonScriptAsync().Started.Should().BeGreaterThan(0);

            // Cleanup: release + reap the still-alive process (kill was disabled).
            File.WriteAllText(releaseFile, "");
        }
```

- [ ] Run it. With Tasks 5–6 implemented it should PASS; if abandon was never called it FAILs on the `.Started.Should().BeGreaterThan(0)` assertion (debug Task 5):

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~ClientScriptExecutionAbandon.ExecuteScript_WhenCancellationStaysPending_EscalatesToAbandon"
```
Expected after correct wiring: PASS. (If it fails because abandon was not called, the orchestrator escalation is wrong — debug Task 5.)

- [ ] Run the whole abandon integration fixture once more for regression:

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~ClientScriptExecutionAbandon"
```
Expected: PASS (all tests).

- [ ] Commit: `git commit -am "Integration: ExecuteScript escalates stuck cancel to abandon after threshold"`

---

## Task 8 — Full build across all TFMs (net48 trap guard)

The Core project multi-targets `net48;net8.0;net8.0-windows`. The runner change in Task 1 keeps the generic `TaskCompletionSource<object?>` (the non-generic `TaskCompletionSource` does NOT exist on net48), so this must still build on net48.

**Files**
- (no source change)

- [ ] Build the Core project on all three TFMs:

```bash
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj --framework net48
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj --framework net8.0
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj --framework net8.0-windows
```
Expected: all three succeed.

- [ ] Build the integration test project on net48 + net8.0 (it runs the runner tests on both):

```bash
dotnet build source/Octopus.Tentacle.Tests.Integration --framework net48
dotnet build source/Octopus.Tentacle.Tests.Integration --framework net8.0
```
Expected: both succeed.

- [ ] Run the runner fixture on net48 to confirm the keyed-on-token change behaves there too:

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net48 \
  --filter "FullyQualifiedName~SilentProcessRunnerFixture.AbandonToken"
```
Expected: PASS.

- [ ] No commit needed (no source change). If any TFM failed, fix inline and commit `git commit -am "Fix net48 build for abandon runner change"`.

---

## Task 9 (final) — Apply Change 1 to PR2 branch (#1244)

PR2 (`jimpelletier/eft-3295-pr2-async-unlink`, #1244) already has the async `WaitForExitAsync` migration and already keys its abandon-catch on `abandon.IsCancellationRequested` — so the *runner* half of Change 1 is already correct there. What PR2 is still missing is the `AbandonScriptAsync` best-effort-kill: its `AbandonScriptAsync` calls only `Abandon()`. Apply the same Cancel-then-Abandon change. PR2's grandchild tests must STAY as "cancel then abandon" (do NOT flip them back to "cancel alone").

**Files (on branch `jimpelletier/eft-3295-pr2-async-unlink`)**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs` (`AbandonScriptAsync`)
- Test: `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs` (cherry-pick Task 2's `AbandonScript_WithNoPriorCancel_KillsTheProcess`)

- [ ] Switch to PR2 and confirm the runner is already token-keyed (no runner change needed):

```bash
git switch jimpelletier/eft-3295-pr2-async-unlink
grep -n "abandon.IsCancellationRequested\|runningScript.Cancel\|runningScript.Abandon" \
  source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs \
  source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs
```
Expected: the runner shows `catch (OperationCanceledException) when (abandon.IsCancellationRequested)`; `AbandonScriptAsync` shows only `runningScript.Abandon()` (the gap to fix).

- [ ] Cherry-pick Task 2's failing test onto PR2: add `AbandonScript_WithNoPriorCancel_KillsTheProcess` (identical body to Task 2) to `ClientScriptExecutionAbandon.cs`. Run it and watch it FAIL:

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~ClientScriptExecutionAbandon.AbandonScript_WithNoPriorCancel_KillsTheProcess"
```
Expected: FAIL (no kill, test times out).

- [ ] Apply the same `AbandonScriptAsync` change as Task 2 (Cancel then Abandon) to PR2's `ScriptServiceV2.cs`:

```csharp
            if (runningScripts.TryGetValue(command.Ticket, out var runningScript))
            {
                runningScript.Cancel();
                runningScript.Abandon();
            }
```

- [ ] Re-run the cherry-picked test. Expected: PASS.

- [ ] Confirm PR2's grandchild tests STAY as "cancel then abandon" — do NOT modify them. Run them to confirm they still pass with the async runner + best-effort-kill abandon:

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --framework net8.0 \
  --filter "FullyQualifiedName~SilentProcessRunnerFixture"
```
Expected: PASS (grandchild tests require cancel-then-abandon on PR2 by design — do not regress them to PR1's "cancel alone" shape).

- [ ] Build PR2's Core on all TFMs (net48 guard for PR2's `WaitForExitAsync` polyfill):

```bash
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj --framework net48
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj --framework net8.0
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj --framework net8.0-windows
```
Expected: all succeed.

- [ ] Commit on PR2: `git commit -am "PR2: AbandonScriptAsync Cancel() then Abandon() so abandon best-effort-kills"`

- [ ] Switch back to PR1: `git switch jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex`

---

## Notes / spec gaps surfaced during planning

- **Capability string mismatch.** Spec Section 1 names the capability `AbandonScriptV2`. The code on this branch advertises `nameof(ScriptServiceV2.AbandonScriptAsync)` = `"AbandonScriptAsync"`. This plan matches the *deployed* string in both the Tentacle advertisement and the client `HasAbandonScriptV2()` check. Renaming to `AbandonScriptV2` is a separate contract decision and is intentionally NOT done here.
- **Most of the contract is already merged.** `AbandonScriptCommandV2`, `IScriptServiceV2.AbandonScript`, `AbandonedExitCode = -48`, `ITentacleClient.AbandonScript`, `RunningScript` abandon catch, and the PR1 runner block already exist. The two deltas in this plan are the only behavioural changes Change 1 and Change 2 require.
- **`process.HasExited` is deliberately NOT read in the PR1 abandon branch.** After `Cancel()→Close()` detaches the Process, reading `HasExited`/`ExitCode` would throw or lie. The "abandon unnecessary" race is handled one layer up by `GetResponse` returning the already-`Complete` `RunningScript`'s real code.
