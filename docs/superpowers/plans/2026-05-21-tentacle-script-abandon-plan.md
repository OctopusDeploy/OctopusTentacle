# Tentacle script abandon — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-05-21-tentacle-script-abandon-design.md`
**Ticket:** [EFT-3295](https://linear.app/octopus/issue/EFT-3295/tentacle-script-abandonment-to-release-the-mutex)

**Goal:** Add an `AbandonScript` verb to `IScriptServiceV2` so Octopus Server can tell Tentacle to release the `ScriptIsolationMutex` and accept new work even when `Process.Kill` failed to stop a stuck script.

**Architecture:** Async migration of `SilentProcessRunner.ExecuteCommand` to `ExecuteCommandAsync`, replacing `process.WaitForExit()` with `await process.WaitForExitAsync(abandon)`. Two-token model on the call chain: existing `cancel` (drives kill via `cancel.Register`) and new `abandon` (drives the wait's early return). New RPC method on `IScriptServiceV2` fires the abandon token. Tentacle does NOT kill the OS process; the runaway is the customer's host-level problem per the ticket.

**Tech stack:** .NET (multi-target), Halibut RPC, NUnit + FluentAssertions, NSubstitute for mocks. PowerShell on Windows, Bash on Linux.

**Working branch:** `jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex` (PR #1226).

---

## File structure

### New files
- `source/Octopus.Tentacle.Contracts/ScriptServiceV2/AbandonScriptCommandV2.cs` — new command DTO.

### Modified — contracts
- `source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs` — add `AbandonedExitCode = -48`.
- `source/Octopus.Tentacle.Contracts/ScriptServiceV2/IScriptServiceV2.cs` — add `AbandonScript` method signature.

### Modified — production code
- `source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs` — `ExecuteCommand` → `ExecuteCommandAsync`; add `abandon` token; swap `WaitForExit()` for `await WaitForExitAsync(abandon)`; abandon catch returns `AbandonedExitCode` after `SafelyCancelRead`.
- `source/Octopus.Tentacle/Util/ISilentProcessRunner.cs` — interface and wrapper become async, add `abandon` parameter.
- `source/Octopus.Tentacle/Util/CommandLineRunner.cs` — caller migration to await.
- `source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs` — `RunScript` → `RunScriptAsync`; constructor accepts `abandonToken`; plumb through.
- `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs` — `LaunchShell` passes `abandonToken`; `RunningScriptWrapper` gains `abandonTokenSource`; new `AbandonScriptAsync`; targeted best-effort `workspace.Delete` in `CompleteScriptAsync`.
- `source/Octopus.Tentacle/Services/Capabilities/CapabilitiesServiceV2.cs` — add `"AbandonScriptV2"` to the non-Kubernetes capability list.
- `source/Octopus.Tentacle.Core/Util/EnvironmentVariables.cs` — add `TentacleDebugDisableProcessKill = "TentacleDebugDisableProcessKill"`.

### Modified — Kubernetes integration test scaffolding (caller migration only)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Tooling/KubeCtlTool.cs` (1 site)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/DockerImageLoader.cs` (2 sites)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesAgentInstaller.cs` (3 sites)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesClusterInstaller.cs` (4 sites)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/HelmDownloader.cs` (1 site)
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/ToolDownloader.cs` (1 site)

### Modified — Tentacle integration test scaffolding (caller migration only)
- `source/Octopus.Tentacle.Tests.Integration/PowerShellStartupDetectionTests.cs` (3 sites)
- `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs` (existing tests need await; abandon tests added)
- `source/Octopus.Tentacle.Tests.Integration/Support/TentacleFetchers/LinuxTentacleFetcher.cs` (1 site)

### New tests
- Additions inside `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs` — abandon-token behaviour, async timing, thread-leak.
- Additions inside `source/Octopus.Tentacle.Tests/Util/RunningScriptFixture.cs` — abandon plumbing.
- Additions inside `source/Octopus.Tentacle.Tests/Integration/ScriptServiceV2Fixture.cs` — service-layer abandon paths.
- New file `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs` — end-to-end mutex-release-on-abandon (mirrors `ClientScriptExecutionIsolationMutex.cs`).

---

## Task ordering rationale

Contracts first (no behaviour change, just shapes). Test affordance next (needed by later integration tests). Async migration is the biggest single change — done in one bottom-up pass with all callers migrated together so the build stays green. RunningScript / ScriptServiceV2 abandon wiring after the async machinery exists. Capability advertisement last (it's a one-line addition gating the whole feature). Tests interleaved with the behaviour they cover.

---

### Task 1: Add `AbandonedExitCode = -48`

**Files:**
- Modify: `source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs`

- [ ] **Step 1: Add the constant**

Open `source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs`. Add a new line right after `PowerShellNeverStartedExitCode = -47;`:

```csharp
public const int AbandonedExitCode = -48;
```

The full block should read:

```csharp
public const int PowerShellNeverStartedExitCode = -47;
public const int AbandonedExitCode = -48;

//Kubernetes Agent
public const int KubernetesScriptPodNotFound = -81;
```

- [ ] **Step 2: Build**

```bash
dotnet build source/Octopus.Tentacle.Contracts/Octopus.Tentacle.Contracts.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs
git commit -m "Add AbandonedExitCode = -48 to ScriptExitCodes"
```

---

### Task 2: Add `AbandonScriptCommandV2` DTO

**Files:**
- Create: `source/Octopus.Tentacle.Contracts/ScriptServiceV2/AbandonScriptCommandV2.cs`

- [ ] **Step 1: Create the file**

Use the same shape as `CancelScriptCommandV2.cs` (which lives in the same folder):

```csharp
using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV2
{
    public class AbandonScriptCommandV2
    {
        public AbandonScriptCommandV2(ScriptTicket ticket, long lastLogSequence)
        {
            Ticket = ticket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public long LastLogSequence { get; }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build source/Octopus.Tentacle.Contracts/Octopus.Tentacle.Contracts.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add source/Octopus.Tentacle.Contracts/ScriptServiceV2/AbandonScriptCommandV2.cs
git commit -m "Add AbandonScriptCommandV2 contract"
```

---

### Task 3: Add `AbandonScript` method to `IScriptServiceV2`

**Files:**
- Modify: `source/Octopus.Tentacle.Contracts/ScriptServiceV2/IScriptServiceV2.cs`

- [ ] **Step 1: Update the interface**

Add `AbandonScript` between `CancelScript` and `CompleteScript`:

```csharp
using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV2
{
    public interface IScriptServiceV2
    {
        ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command);
        ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request);
        ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command);
        ScriptStatusResponseV2 AbandonScript(AbandonScriptCommandV2 command);
        void CompleteScript(CompleteScriptCommandV2 command);
    }
}
```

- [ ] **Step 2: Build the whole solution**

```bash
dotnet build source/Tentacle.sln
```

Expected: **build fails.** The async implementer (`ScriptServiceV2` in `Octopus.Tentacle.Core`) doesn't implement the new method yet. That's intentional — we'll fix it in Task 11. For now, capture the compile errors and confirm they're the expected "missing implementation" errors and nothing else.

- [ ] **Step 3: Stash the stub on Halibut decorators**

Tentacle wraps services with async decorators (look for `IAsyncScriptServiceV2`, `BackwardsCompatibleAsyncCapabilitiesV2Decorator`, etc). For the build to stay green between Task 3 and Task 11, add a **temporary** `NotImplementedException`-throwing stub to `ScriptServiceV2.cs`:

Open `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs`. Add this method right after `CancelScriptAsync`:

```csharp
public async Task<ScriptStatusResponseV2> AbandonScriptAsync(AbandonScriptCommandV2 command, CancellationToken cancellationToken)
{
    await Task.CompletedTask;
    throw new NotImplementedException("Implemented in Task 11");
}
```

- [ ] **Step 4: Build again, confirm green**

```bash
dotnet build source/Tentacle.sln
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add source/Octopus.Tentacle.Contracts/ScriptServiceV2/IScriptServiceV2.cs source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs
git commit -m "Add AbandonScript to IScriptServiceV2 interface (stub)"
```

---

### Task 4: Add `TentacleDebugDisableProcessKill` env-var constant

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Util/EnvironmentVariables.cs`

- [ ] **Step 1: Add the constant**

Open the file. Add a new line in the `EnvironmentVariables` static class, grouped near the other `Tentacle*` constants:

```csharp
public const string TentacleDebugDisableProcessKill = "TentacleDebugDisableProcessKill";
```

- [ ] **Step 2: Build**

```bash
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add source/Octopus.Tentacle.Core/Util/EnvironmentVariables.cs
git commit -m "Add TentacleDebugDisableProcessKill env var constant"
```

(The Hitman wiring happens in Task 6 alongside the async migration so the test affordance is in place before any new tests need it.)

---

### Task 5: Make `SilentProcessRunner.ExecuteCommand` async — failing test first

**Files:**
- Modify: `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs`

This is TDD's red step for the async migration. We're not going to migrate the whole call chain yet — we just write the new test that targets the future async method so it fails to compile, proving we need the new signature.

- [ ] **Step 1: Add the failing test**

Open `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs`. Add this new test near the existing `CancellationToken_*` tests:

```csharp
[Test]
public async Task AbandonToken_ShouldReturnAbandonedExitCodeWithoutKillingProcess()
{
    var command = PlatformDetection.IsRunningOnWindows ? "powershell.exe" : "/bin/bash";
    var arguments = PlatformDetection.IsRunningOnWindows
        ? "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 300\""
        : "-c \"sleep 300\"";

    using var cancelCts = new CancellationTokenSource();
    using var abandonCts = new CancellationTokenSource();

    var infoMessages = new StringBuilder();

    var sw = Stopwatch.StartNew();

    var task = Task.Run(async () => await SilentProcessRunner.ExecuteCommandAsync(
        command,
        arguments,
        Environment.CurrentDirectory,
        debug: _ => { },
        info: msg => { lock (infoMessages) infoMessages.AppendLine(msg); },
        error: _ => { },
        customEnvironmentVariables: null,
        cancel: cancelCts.Token,
        abandon: abandonCts.Token));

    // Give the process ~500ms to actually start before we abandon
    await Task.Delay(500);
    abandonCts.Cancel();

    var exitCode = await task;
    sw.Stop();

    sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "abandon should return promptly");
    exitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
    infoMessages.ToString().Should().Contain("Tentacle has abandoned this script");
}
```

Add the corresponding `using`s at the top if missing:

```csharp
using System.Diagnostics;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
```

- [ ] **Step 2: Confirm it fails to compile**

```bash
dotnet build source/Octopus.Tentacle.Tests.Integration/Octopus.Tentacle.Tests.Integration.csproj
```

Expected: compile error referencing `ExecuteCommandAsync` not existing on `SilentProcessRunner`. That's the red.

- [ ] **Step 3: Commit (red phase)**

```bash
git add source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs
git commit -m "Add failing test for AbandonToken behaviour in SilentProcessRunner"
```

We commit red because the next task migrates the production method; both will pass together once the migration completes.

---

### Task 6: Migrate `SilentProcessRunner.ExecuteCommand` to async + add `abandon` token + plumb `TentacleDebugDisableProcessKill`

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs`

This is the load-bearing implementation task. We rename the method, change the return to `Task<int>`, add the `abandon` parameter, swap `process.WaitForExit()` for `await process.WaitForExitAsync(abandon)`, add the abandon catch with `SafelyCancelRead` + honest log line + `AbandonedExitCode`, and wire the env var into `Hitman.TryKillProcessAndChildrenRecursively`.

- [ ] **Step 1: Update `ExecuteCommand` signature and body**

Find the current `public static int ExecuteCommand(...)` overload at the top (around line 17). Update both overloads to be `async Task<int>` and add the `abandon` parameter. The simpler overload should delegate to the richer one:

```csharp
public static Task<int> ExecuteCommandAsync(
    string executable,
    string arguments,
    string workingDirectory,
    Action<string> debug,
    Action<string> info,
    Action<string> error,
    CancellationToken cancel,
    CancellationToken abandon)
{
    return ExecuteCommandAsync(executable, arguments, workingDirectory, debug, info, error, customEnvironmentVariables: null, cancel: cancel, abandon: abandon);
}

public static async Task<int> ExecuteCommandAsync(
    string executable,
    string arguments,
    string workingDirectory,
    Action<string> debug,
    Action<string> info,
    Action<string> error,
    IReadOnlyDictionary<string, string>? customEnvironmentVariables = null,
    CancellationToken cancel = default,
    CancellationToken abandon = default)
{
    // ... existing argument-null checks ...
    // ... existing process.StartInfo setup ...
    process.Start();

    var running = true;

    using (cancel.Register(() =>
           {
               if (running) DoOurBestToCleanUp(process, error);
           }))
    {
        if (cancel.IsCancellationRequested)
            DoOurBestToCleanUp(process, error);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(abandon).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (abandon.IsCancellationRequested && !process.HasExited)
        {
            info("Tentacle has abandoned this script. The underlying script process may still be running on this host.");
            SafelyCancelRead(process.CancelErrorRead, debug);
            SafelyCancelRead(process.CancelOutputRead, debug);
            running = false;
            return ScriptExitCodes.AbandonedExitCode;
        }

        SafelyCancelRead(process.CancelErrorRead, debug);
        SafelyCancelRead(process.CancelOutputRead, debug);

        SafelyWaitForAllOutput(outputResetEvent, cancel, debug);
        SafelyWaitForAllOutput(errorResetEvent, cancel, debug);

        var exitCode = SafelyGetExitCode(process);
        debug($"Process {exeFileNameOrFullPath} in {workingDirectory} exited with code {exitCode}");

        running = false;
        return exitCode;
    }
}
```

Notes:
- The old synchronous `ExecuteCommand` overloads are deleted. Every caller migrates in Tasks 7–9.
- `running = false` set inside the abandon catch as well — `cancel.Register`'s callback checks `running` to decide whether to call `DoOurBestToCleanUp`. After abandon we don't want it firing.

- [ ] **Step 2: Wire `TentacleDebugDisableProcessKill` into `Hitman`**

In the same file, find the `Hitman.TryKillProcessAndChildrenRecursively` method (around line 250). Add the env-var check at the top:

```csharp
public static void TryKillProcessAndChildrenRecursively(Process process)
{
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill)))
    {
        // Test-only no-op: simulate "kill was attempted but didn't terminate the process".
        // Only activated when the test harness sets this env var on the Tentacle process.
        return;
    }

#if NETFRAMEWORK
    TryKillWindowsProcessAndChildrenRecursively(process.Id);
#endif
#if !NETFRAMEWORK
    process.Kill(true);
#endif
}
```

Add the `using` at the top if not already present:

```csharp
using Octopus.Tentacle.Core.Util;
```

- [ ] **Step 3: Build (expect cascade failures from removed sync method)**

```bash
dotnet build source/Tentacle.sln
```

Expected: many compile errors at every caller of the removed `ExecuteCommand`. That's the next several tasks.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs
git commit -m "Migrate SilentProcessRunner to async; add abandon token; debug kill-disable flag"
```

---

### Task 7: Migrate production callers to await

**Files:**
- Modify: `source/Octopus.Tentacle/Util/ISilentProcessRunner.cs`
- Modify: `source/Octopus.Tentacle/Util/CommandLineRunner.cs`

- [ ] **Step 1: Update `ISilentProcessRunner` interface and wrapper**

Open `source/Octopus.Tentacle/Util/ISilentProcessRunner.cs`. Make the interface and wrapper async, add `abandon`:

```csharp
public interface ISilentProcessRunner
{
    Task<int> ExecuteCommandAsync(
        string executable,
        string arguments,
        string workingDirectory,
        Action<string> info,
        Action<string> error,
        CancellationToken cancel = default,
        CancellationToken abandon = default);

    Task<int> ExecuteCommandAsync(
        string executable,
        string arguments,
        string workingDirectory,
        Action<string> debug,
        Action<string> info,
        Action<string> error,
        CancellationToken cancel = default,
        CancellationToken abandon = default);
}

public class SilentProcessRunnerWrapper : ISilentProcessRunner
{
    public Task<int> ExecuteCommandAsync(string executable, string arguments, string workingDirectory, Action<string> info, Action<string> error, CancellationToken cancel = default, CancellationToken abandon = default)
    {
        return SilentProcessRunnerExtended.ExecuteCommandAsync(executable, arguments, workingDirectory, info, error, cancel, abandon);
    }

    public Task<int> ExecuteCommandAsync(string executable, string arguments, string workingDirectory, Action<string> debug, Action<string> info, Action<string> error, CancellationToken cancel = default, CancellationToken abandon = default)
    {
        return SilentProcessRunner.ExecuteCommandAsync(executable, arguments, workingDirectory, debug, info, error, cancel: cancel, abandon: abandon);
    }
}
```

Update the `SilentProcessRunnerExtended` static helpers in the same file. The extension methods on `CommandLineInvocation` will need to become async too:

```csharp
public static async Task<CmdResult> ExecuteCommandAsync(this CommandLineInvocation invocation)
    => await ExecuteCommandAsync(invocation, Environment.CurrentDirectory);

public static async Task<CmdResult> ExecuteCommandAsync(this CommandLineInvocation invocation, string workingDirectory)
{
    if (workingDirectory == null)
        throw new ArgumentNullException(nameof(workingDirectory));

    var arguments = $"{invocation.Arguments} {invocation.SystemArguments ?? string.Empty}";
    var infos = new List<string>();
    var errors = new List<string>();

    var exitCode = await ExecuteCommandAsync(
        invocation.Executable,
        arguments,
        workingDirectory,
        infos.Add,
        errors.Add
    );

    return new CmdResult(exitCode, infos, errors);
}

public static Task<int> ExecuteCommandAsync(
    string executable,
    string arguments,
    string workingDirectory,
    Action<string> info,
    Action<string> error,
    CancellationToken cancel = default,
    CancellationToken abandon = default)
    => SilentProcessRunner.ExecuteCommandAsync(executable,
        arguments,
        workingDirectory,
        LogFileOnlyLogger.Current.Info,
        info,
        error,
        customEnvironmentVariables: null,
        cancel: cancel,
        abandon: abandon);
```

- [ ] **Step 2: Migrate `CommandLineRunner`**

Open `source/Octopus.Tentacle/Util/CommandLineRunner.cs`. Find the call to `SilentProcessRunner.ExecuteCommand` and convert. The whole method becomes async — propagate the change up the chain until you reach a natural async boundary or `Task.Run` / `.GetAwaiter().GetResult()` glue is needed.

Pattern for each call site:

```csharp
// Before:
var exitCode = SilentProcessRunner.ExecuteCommand(invocation.Executable, ...);
// After:
var exitCode = await SilentProcessRunner.ExecuteCommandAsync(invocation.Executable, ..., abandon: CancellationToken.None);
```

For `CommandLineRunner.Execute`, the method becomes `ExecuteAsync` returning `Task<int>`. Any caller that hits a sync boundary uses `.GetAwaiter().GetResult()` *as a last resort, with a comment explaining why*.

- [ ] **Step 3: Build**

```bash
dotnet build source/Octopus.Tentacle/Octopus.Tentacle.csproj
```

Expected: build succeeds (or surfaces the next layer of callers; resolve them with the same pattern).

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle/Util/ISilentProcessRunner.cs source/Octopus.Tentacle/Util/CommandLineRunner.cs
git commit -m "Migrate ISilentProcessRunner and CommandLineRunner to async"
```

---

### Task 8: Migrate Kubernetes integration test scaffolding to await

**Files:**
- Modify (caller migration only):
  - `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Tooling/KubeCtlTool.cs`
  - `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/DockerImageLoader.cs`
  - `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesAgentInstaller.cs`
  - `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesClusterInstaller.cs`
  - `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/HelmDownloader.cs`
  - `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/ToolDownloader.cs`

- [ ] **Step 1: Apply the same caller pattern to every call site**

Pattern at each `SilentProcessRunner.ExecuteCommand(...)`:

```csharp
// Before (synchronous):
var exitCode = SilentProcessRunner.ExecuteCommand(executable, args, workingDir, debug, info, error, cancel: ct);
// After (async, abandon-token = None because these are setup tools, not Tentacle script execution):
var exitCode = await SilentProcessRunner.ExecuteCommandAsync(executable, args, workingDir, debug, info, error, cancel: ct, abandon: CancellationToken.None);
```

Make the containing method `async Task<int>` (or `async Task` if it doesn't return the exit code). Propagate `async` up the call chain in this file. Most of these scaffolding methods are already called from `async` test setup, so the propagation is usually one or two layers.

For commented-out lines (e.g. `KubernetesClusterInstaller.cs:129`), leave them commented.

- [ ] **Step 2: Build the K8s integration test project**

```bash
dotnet build source/Octopus.Tentacle.Kubernetes.Tests.Integration/Octopus.Tentacle.Kubernetes.Tests.Integration.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add source/Octopus.Tentacle.Kubernetes.Tests.Integration/
git commit -m "Migrate Kubernetes integration test scaffolding to async ExecuteCommandAsync"
```

---

### Task 9: Migrate Tentacle integration test scaffolding to await

**Files:**
- Modify:
  - `source/Octopus.Tentacle.Tests.Integration/PowerShellStartupDetectionTests.cs` (3 sites)
  - `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs` (existing sync tests — but also fix the helper there)
  - `source/Octopus.Tentacle.Tests.Integration/Support/TentacleFetchers/LinuxTentacleFetcher.cs`

- [ ] **Step 1: Migrate each caller**

Same pattern as Task 8. `SilentProcessRunner.ExecuteCommand(...)` → `await SilentProcessRunner.ExecuteCommandAsync(..., abandon: CancellationToken.None)`. Containing methods become `async Task<...>`.

In `SilentProcessRunnerFixture.cs`, there's a private helper near the top that wraps `ExecuteCommand` for the existing tests (`Execute(...)`). Migrate it:

```csharp
static async Task<int> ExecuteAsync(string command, string arguments, string workingDirectory, out StringBuilder debugMessages, out StringBuilder infoMessages, out StringBuilder errorMessages, CancellationToken cancel = default, CancellationToken abandon = default)
```

Each existing test that calls `Execute(...)` now calls `await ExecuteAsync(...)`. Tests become `async Task` returning methods. NUnit handles that.

- [ ] **Step 2: Build the Tentacle integration test project**

```bash
dotnet build source/Octopus.Tentacle.Tests.Integration/Octopus.Tentacle.Tests.Integration.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Run the existing SilentProcessRunner tests on Linux + Windows**

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --filter "FullyQualifiedName~SilentProcessRunnerFixture"
```

Expected: all existing tests pass. The new `AbandonToken_ShouldReturnAbandonedExitCodeWithoutKillingProcess` test from Task 5 ALSO passes now that the production method exists. Green.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle.Tests.Integration/
git commit -m "Migrate Tentacle integration test scaffolding to async; AbandonToken test now passes"
```

---

### Task 10: Add abandon support to `RunningScript`

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs`
- Modify: `source/Octopus.Tentacle.Tests/Util/RunningScriptFixture.cs` (or wherever the existing fixture lives — adjust path if it's in `Octopus.Tentacle.Tests.Integration`)

- [ ] **Step 1: Write the failing test**

Open the existing `RunningScriptFixture.cs`. Add a test that exercises the abandon path:

```csharp
[Test]
public async Task Execute_WhenAbandonTokenFires_ReturnsAbandonedExitCode()
{
    // arrange: a workspace + shell that runs a long-sleeping script
    var workspace = CreateWorkspace(bashScript: "sleep 300", powershellScript: "Start-Sleep -Seconds 300");
    var shell = new Bash(); // or appropriate cross-platform helper
    using var runningCts = new CancellationTokenSource();
    using var abandonCts = new CancellationTokenSource();

    var runningScript = new RunningScript(
        shell,
        workspace,
        stateStore: null,
        scriptLog: workspace.CreateLog(),
        taskId: "ServerTask-1",
        scriptIsolationMutex: new ScriptIsolationMutex(),
        runningScriptToken: runningCts.Token,
        abandonToken: abandonCts.Token,
        environmentVariables: new Dictionary<string, string>(),
        powerShellStartupTimeout: TimeSpan.FromMinutes(1),
        log: Substitute.For<ISystemLog>());

    var executeTask = runningScript.Execute();
    await Task.Delay(500); // let the process start
    abandonCts.Cancel();

    await executeTask;
    runningScript.State.Should().Be(ProcessState.Complete);
    runningScript.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
}
```

Run it; expect compile failure ("RunningScript constructor doesn't accept abandonToken").

- [ ] **Step 2: Add `abandonToken` to `RunningScript`**

In `RunningScript.cs`, add a field and constructor parameter:

```csharp
readonly CancellationToken runningScriptToken;
readonly CancellationToken abandonToken; // NEW

public RunningScript(IShell shell,
    IScriptWorkspace workspace,
    IScriptStateStore? stateStore,
    IScriptLog scriptLog,
    string taskId,
    ScriptIsolationMutex scriptIsolationMutex,
    CancellationToken runningScriptToken,
    CancellationToken abandonToken, // NEW
    IReadOnlyDictionary<string, string> environmentVariables,
    TimeSpan powerShellStartupTimeout,
    ILog log)
{
    // ... existing assignments ...
    this.abandonToken = abandonToken;
    // ...
}
```

Update the secondary constructor that omits `stateStore` to pass `abandonToken` through as well.

- [ ] **Step 3: Replace `RunScript` with async, plumb `abandonToken`**

Replace the existing `int RunScript(string shellPath, IScriptLogWriter writer, CancellationToken cancellationToken)` with:

```csharp
async Task<int> RunScriptAsync(string shellPath, IScriptLogWriter writer, CancellationToken cancellationToken, CancellationToken abandon)
{
    try
    {
        var exitCode = await SilentProcessRunner.ExecuteCommandAsync(
            shellPath,
            shell.FormatCommandArguments(workspace.BootstrapScriptFilePath, workspace.ScriptArguments, false),
            workspace.WorkingDirectory,
            LogScriptOutputTo(writer, ProcessOutputSource.Debug),
            LogScriptOutputTo(writer, ProcessOutputSource.StdOut),
            LogScriptOutputTo(writer, ProcessOutputSource.StdErr),
            environmentVariables,
            cancellationToken,
            abandon);

        return exitCode;
    }
    catch (Exception ex)
    {
        writer.WriteOutput(ProcessOutputSource.StdErr, "An exception was thrown when invoking " + shellPath + ": " + ex.Message);
        writer.WriteOutput(ProcessOutputSource.StdErr, ex.ToString());
        return ScriptExitCodes.PowershellInvocationErrorExitCode;
    }
}
```

- [ ] **Step 4: Update `Execute` to await the async `RunScriptAsync`**

Inside `Execute()`, change the call:

```csharp
exitCode = workspace.ShouldMonitorPowerShellStartup()
    ? await RunPowershellScriptWithMonitoring(shellPath, writer, runningScriptToken)
    : await RunScriptAsync(shellPath, writer, runningScriptToken, abandonToken);
```

Inside `RunPowershellScriptWithMonitoring`, find the `Task.Run(() => RunScript(...))` line and change to `Task.Run(async () => await RunScriptAsync(shellPath, writer, scriptTaskCts.Token, abandonToken), scriptTaskCts.Token)`.

- [ ] **Step 5: Build and run the new test**

```bash
dotnet build source/Tentacle.sln
dotnet test source/Octopus.Tentacle.Tests.Integration --filter "Execute_WhenAbandonTokenFires"
```

Expected: build succeeds; the new test passes. Build of the broader solution will surface that `ScriptServiceV2.cs` doesn't pass `abandonToken` yet — that's Task 11.

- [ ] **Step 6: Commit**

```bash
git add source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs source/Octopus.Tentacle.Tests/Util/RunningScriptFixture.cs
git commit -m "Plumb abandon token through RunningScript; covered by new test"
```

---

### Task 11: Implement `ScriptServiceV2.AbandonScriptAsync` and add `abandonTokenSource` to wrapper

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs`
- Modify: `source/Octopus.Tentacle.Tests/Integration/ScriptServiceV2Fixture.cs` (existing fixture)

- [ ] **Step 1: Write failing service-layer tests**

In `ScriptServiceV2Fixture.cs`, add these tests:

```csharp
[Test]
public async Task AbandonScript_OnUnknownTicket_ReturnsCompleteWithUnknownScriptExitCode()
{
    var service = CreateService();
    var ticket = new ScriptTicket("unknown");
    var response = await service.AbandonScriptAsync(new AbandonScriptCommandV2(ticket, 0), CancellationToken.None);

    response.State.Should().Be(ProcessState.Complete);
    response.ExitCode.Should().Be(ScriptExitCodes.UnknownScriptExitCode);
}

[Test]
public async Task AbandonScript_OnRunningScript_FiresAbandonToken_ReleasesMutex_ReturnsAbandonedExitCode()
{
    var service = CreateService();

    // start a script that will block on a file-wait, so it stays Running until we release it
    var startCommand = BuildLongRunningCommand(); // uses TestExecuteShellScriptCommandBuilder
    await service.StartScriptAsync(startCommand, CancellationToken.None);

    var response = await service.AbandonScriptAsync(
        new AbandonScriptCommandV2(startCommand.ScriptTicket, 0),
        CancellationToken.None);

    response.State.Should().Be(ProcessState.Complete);
    response.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

    // mutex should be free: a new FullIsolation script should start
    var second = BuildLongRunningCommand();
    var secondResponse = await service.StartScriptAsync(second, CancellationToken.None);
    secondResponse.State.Should().NotBe(ProcessState.Pending); // i.e. wasn't blocked on the mutex
}

[Test]
public async Task AbandonScript_OnAlreadyCompletedScript_ReturnsRealExitCodeNotAbandoned()
{
    var service = CreateService();
    var startCommand = BuildShortRunningCommand(exitCode: 0); // completes quickly

    await service.StartScriptAsync(startCommand, CancellationToken.None);

    // wait for completion
    ScriptStatusResponseV2 status;
    do { status = await service.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None); }
    while (status.State != ProcessState.Complete);

    var abandonResponse = await service.AbandonScriptAsync(new AbandonScriptCommandV2(startCommand.ScriptTicket, 0), CancellationToken.None);
    abandonResponse.ExitCode.Should().Be(0, "real exit code should be returned, not AbandonedExitCode");
}
```

Run: expect compile failures (the stub from Task 3 throws NotImplementedException; the assertions will fail).

- [ ] **Step 2: Implement `AbandonScriptAsync`**

Replace the Task 3 stub with the real implementation. Also add `abandonTokenSource` to `RunningScriptWrapper`:

```csharp
class RunningScriptWrapper : IDisposable
{
    readonly CancellationTokenSource cancellationTokenSource = new();
    readonly CancellationTokenSource abandonTokenSource = new();

    public RunningScriptWrapper(ScriptStateStore scriptStateStore)
    {
        ScriptStateStore = scriptStateStore;
        CancellationToken = cancellationTokenSource.Token;
        AbandonToken = abandonTokenSource.Token;
    }

    public RunningScript? Process { get; set; }
    public ScriptStateStore ScriptStateStore { get; }
    public SemaphoreSlim StartScriptMutex { get; } = new(1, 1);

    public CancellationToken CancellationToken { get; }
    public CancellationToken AbandonToken { get; }

    public void Cancel() => cancellationTokenSource.Cancel();
    public void Abandon() => abandonTokenSource.Cancel();

    public void Dispose()
    {
        cancellationTokenSource.Dispose();
        abandonTokenSource.Dispose();
    }
}
```

Replace the stub `AbandonScriptAsync`:

```csharp
public async Task<ScriptStatusResponseV2> AbandonScriptAsync(AbandonScriptCommandV2 command, CancellationToken cancellationToken)
{
    await Task.CompletedTask;

    if (runningScripts.TryGetValue(command.Ticket, out var runningScript))
    {
        runningScript.Abandon();
    }

    return GetResponse(command.Ticket, command.LastLogSequence, runningScript?.Process);
}
```

In `LaunchShell`, pass `abandonToken` through:

```csharp
RunningScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, IScriptStateStore stateStore, CancellationToken cancellationToken, CancellationToken abandonToken)
{
    var runningScript = new RunningScript(shell, workspace, stateStore, workspace.CreateLog(), serverTaskId, scriptIsolationMutex, cancellationToken, abandonToken, environmentVariables, powerShellStartupTimeout, log);
    _ = Task.Run(async () => await runningScript.Execute());
    return runningScript;
}
```

Update the call site of `LaunchShell` in `StartScriptAsync` to pass `runningScript.AbandonToken`.

- [ ] **Step 3: Run the new tests**

```bash
dotnet test source/Octopus.Tentacle.Tests --filter "FullyQualifiedName~ScriptServiceV2Fixture.AbandonScript"
```

Expected: all three new tests pass.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs source/Octopus.Tentacle.Tests/Integration/ScriptServiceV2Fixture.cs
git commit -m "Implement ScriptServiceV2.AbandonScriptAsync with abandon-token wrapper"
```

---

### Task 12: Targeted best-effort `CompleteScript`

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs`
- Modify: `source/Octopus.Tentacle.Tests/Integration/ScriptServiceV2Fixture.cs`

- [ ] **Step 1: Write a failing test**

Add to `ScriptServiceV2Fixture.cs`:

```csharp
[Test]
public async Task CompleteScript_AfterAbandon_WhenWorkspaceDeleteFails_LogsWarnAndReturnsNormally()
{
    var service = CreateService(); // factory should let us inject a workspace whose Delete throws IOException
    var startCommand = BuildLongRunningCommand();
    await service.StartScriptAsync(startCommand, CancellationToken.None);
    await service.AbandonScriptAsync(new AbandonScriptCommandV2(startCommand.ScriptTicket, 0), CancellationToken.None);

    // arrange the workspace.Delete to fail
    ArrangeWorkspaceDeleteToThrow(startCommand.ScriptTicket, new IOException("file in use"));

    var complete = async () => await service.CompleteScriptAsync(new CompleteScriptCommandV2(startCommand.ScriptTicket, 0), CancellationToken.None);

    await complete.Should().NotThrowAsync();
    // assert: systemLog received a Warn entry mentioning the leaked directory
    fakeSystemLog.WarnMessages.Should().Contain(m => m.Contains("Could not delete") && m.Contains(startCommand.ScriptTicket.TaskId));
}

[Test]
public async Task CompleteScript_AfterNormalCompletion_WhenWorkspaceDeleteFails_PropagatesException()
{
    var service = CreateService();
    var startCommand = BuildShortRunningCommand(exitCode: 0);
    await service.StartScriptAsync(startCommand, CancellationToken.None);

    // poll until natural completion
    ScriptStatusResponseV2 status;
    var deadline = DateTime.UtcNow.AddSeconds(30);
    do
    {
        status = await service.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None);
        if (status.State == ProcessState.Complete) break;
        await Task.Delay(50);
    } while (DateTime.UtcNow < deadline);
    status.State.Should().Be(ProcessState.Complete);
    status.ExitCode.Should().Be(0, "the script exited cleanly, not via abandon");

    ArrangeWorkspaceDeleteToThrow(startCommand.ScriptTicket, new IOException("file in use"));

    var complete = async () => await service.CompleteScriptAsync(new CompleteScriptCommandV2(startCommand.ScriptTicket, 0), CancellationToken.None);

    await complete.Should().ThrowAsync<IOException>();
}
```

Run: expect both to fail (current code propagates the exception unconditionally).

- [ ] **Step 2: Update `CompleteScriptAsync`**

Replace the existing implementation:

```csharp
public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, CancellationToken cancellationToken)
{
    if (runningScripts.TryRemove(command.Ticket, out var runningScript))
    {
        runningScript.Dispose();
    }

    var workspace = workspaceFactory.GetWorkspace(command.Ticket, WorkspaceReadinessCheck.Skip);

    var stateStore = scriptStateStoreFactory.Create(workspace);
    var wasAbandoned = stateStore.Exists()
                      && stateStore.Load().ExitCode == ScriptExitCodes.AbandonedExitCode;

    if (wasAbandoned)
    {
        try
        {
            await workspace.Delete(cancellationToken);
        }
        catch (Exception ex)
        {
            log.Warn(ex, $"Could not delete abandoned workspace at {workspace.WorkingDirectory}. Leaving on disk; the underlying script process may still hold open file handles.");
        }
    }
    else
    {
        await workspace.Delete(cancellationToken);
    }
}
```

- [ ] **Step 3: Run the new tests**

```bash
dotnet test source/Octopus.Tentacle.Tests --filter "FullyQualifiedName~ScriptServiceV2Fixture.CompleteScript"
```

Expected: both new tests pass; existing CompleteScript tests still pass.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs source/Octopus.Tentacle.Tests/Integration/ScriptServiceV2Fixture.cs
git commit -m "Best-effort workspace.Delete gated on AbandonedExitCode"
```

---

### Task 13: Advertise `AbandonScriptV2` capability

**Files:**
- Modify: `source/Octopus.Tentacle/Services/Capabilities/CapabilitiesServiceV2.cs`
- Modify: `source/Octopus.Tentacle.Tests/Capabilities/CapabilitiesServiceV2Fixture.cs` (existing fixture)

- [ ] **Step 1: Write the failing test**

In `CapabilitiesServiceV2Fixture.cs`:

```csharp
[Test]
public async Task GetCapabilities_OnNonKubernetesTentacle_AdvertisesAbandonScriptV2()
{
    var service = new CapabilitiesServiceV2();
    var response = await service.GetCapabilitiesAsync(CancellationToken.None);
    response.SupportedCapabilities.Should().Contain("AbandonScriptV2");
}

[Test]
public async Task GetCapabilities_OnKubernetesTentacle_DoesNotAdvertiseAbandonScriptV2()
{
    // arrange KubernetesSupportDetection.IsRunningAsKubernetesAgent = true (test-only override; mirror existing pattern in the fixture)
    var service = new CapabilitiesServiceV2();
    var response = await service.GetCapabilitiesAsync(CancellationToken.None);
    response.SupportedCapabilities.Should().NotContain("AbandonScriptV2");
}
```

Run: expect both to fail.

- [ ] **Step 2: Add the capability string**

In `CapabilitiesServiceV2.cs`:

```csharp
return new CapabilitiesResponseV2(new List<string>
{
    nameof(IScriptService),
    nameof(IFileTransferService),
    nameof(IScriptServiceV2),
    "AbandonScriptV2"
});
```

- [ ] **Step 3: Run the tests**

```bash
dotnet test source/Octopus.Tentacle.Tests --filter "FullyQualifiedName~CapabilitiesServiceV2Fixture.GetCapabilities"
```

Expected: both new tests pass; existing capability tests still pass.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle/Services/Capabilities/CapabilitiesServiceV2.cs source/Octopus.Tentacle.Tests/Capabilities/CapabilitiesServiceV2Fixture.cs
git commit -m "Advertise AbandonScriptV2 capability"
```

---

### Task 14: Integration test — mutex release on abandon

**Files:**
- Create: `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs`

This is the load-bearing end-to-end test. Mirrors `ClientScriptExecutionIsolationMutex.cs`. Uses the existing builders (`TestExecuteShellScriptCommandBuilder`, `ScriptBuilder`, `Wait.For`, `TentacleServiceDecoratorBuilder`) — do NOT use raw shell + `Thread.Sleep`.

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionAbandon : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.V2)]
        public async Task AbandonScript_WhileScriptIsRunning_ReleasesMutexAndReturnsAbandonedExitCode(TentacleConfigurationTestCase tcc)
        {
            await using var clientTentacle = await tcc.CreateBuilder()
                .WithTentacleEnvironmentVariable("TentacleDebugDisableProcessKill", "1") // make Hitman a no-op
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");

            // first script: signals "started" then blocks until release file appears
            var firstCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName("abandon-test-mutex")
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;

            var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstCommand, CancellationToken));

            // wait until the first script is actually running
            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("first script did not start"),
                CancellationToken);

            // cancel first (kill is mocked off, so the script keeps running)
            await tentacleClient.ScriptServiceV2.CancelScriptAsync(new CancelScriptCommandV2(firstCommand.ScriptTicket, 0), CancellationToken);

            // give cancel a moment to be attempted; then abandon
            await Task.Delay(TimeSpan.FromSeconds(1));

            var abandonResponse = await tentacleClient.ScriptServiceV2.AbandonScriptAsync(new AbandonScriptCommandV2(firstCommand.ScriptTicket, 0), CancellationToken);
            abandonResponse.State.Should().Be(ProcessState.Complete);
            abandonResponse.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

            // load-bearing: second FullIsolation script should now start, proving the mutex was released
            var secondStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "second-start");
            var secondCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().CreateFile(secondStartFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName("abandon-test-mutex")
                .Build();

            var secondResult = await tentacleClient.ExecuteScript(secondCommand, CancellationToken);
            secondResult.response.ExitCode.Should().Be(0);
            File.Exists(secondStartFile).Should().BeTrue();

            // release the first script so the test process doesn't leak forever
            File.WriteAllText(releaseFile, "");
        }
    }
}
```

If `WithTentacleEnvironmentVariable` doesn't exist on the builder, add it as a small helper in `ClientAndTentacleBuilder` and propagate to the Tentacle process startup environment.

- [ ] **Step 2: Run the new test on Linux**

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --filter "ClientScriptExecutionAbandon"
```

Expected: passes.

- [ ] **Step 3: Run on Windows CI**

Push to the branch and verify the Windows CI job passes.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs
git commit -m "Integration test: AbandonScript releases mutex when kill mocked off"
```

---

### Task 15: Integration test — multi-level-deep hang variant

**Files:**
- Modify: `source/Octopus.Tentacle.CommonTestUtils/Builders/ScriptBuilder.cs` (add `AppendRaw` helper)
- Modify: `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs` (add second test)

- [ ] **Step 0: Add `AppendRaw` to `ScriptBuilder`**

The existing `ScriptBuilder` doesn't have a way to inject shell-specific raw command lines. Add this helper near `Print` / `Sleep`:

```csharp
public ScriptBuilder AppendRaw(string bash, string windows)
{
    bashScript.AppendLine(bash);
    windowsScript.AppendLine(windows);
    return this;
}
```

Commit this separately so the helper is available before the multi-level test depends on it:

```bash
git add source/Octopus.Tentacle.CommonTestUtils/Builders/ScriptBuilder.cs
git commit -m "Add ScriptBuilder.AppendRaw for shell-specific command injection"
```

The ticket explicitly asks for a "multi-level-deep hang (bootstrap → Calamari → script → AV)" test.

- [ ] **Step 1: Add the test**

```csharp
[Test]
[TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.V2)]
public async Task AbandonScript_MultiLevelDeepHang_StillReleasesMutex(TentacleConfigurationTestCase tcc)
{
    await using var clientTentacle = await tcc.CreateBuilder()
        .WithTentacleEnvironmentVariable("TentacleDebugDisableProcessKill", "1")
        .Build(CancellationToken);

    var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
    var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");

    // Multi-level chain: Tentacle runs the outer shell (bootstrap), which launches a child shell
    // which itself launches a grandchild that polls for the release file. Mirrors
    // bootstrap → Calamari → user script.
    var script = new ScriptBuilder()
        .CreateFile(startFile)
        .AppendRaw(
            bash: $"bash -c \"bash -c 'while [ ! -f {releaseFile.Replace("\\", "/")} ]; do sleep 0.5; done'\"",
            windows: $"powershell -NoProfile -Command \"powershell -NoProfile -Command 'while (-not (Test-Path \\\"{releaseFile}\\\")) {{ Start-Sleep -Milliseconds 500 }}'\"");

    var command = new TestExecuteShellScriptCommandBuilder()
        .SetScriptBody(script)
        .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
        .WithIsolationMutexName("abandon-multilevel-mutex")
        .Build();

    var tentacleClient = clientTentacle.TentacleClient;
    var firstExecution = Task.Run(async () => await tentacleClient.ExecuteScript(command, CancellationToken));
    await Wait.For(() => File.Exists(startFile),
        TimeSpan.FromSeconds(30),
        () => throw new Exception("multi-level script did not start"),
        CancellationToken);

    await tentacleClient.ScriptServiceV2.CancelScriptAsync(new CancelScriptCommandV2(command.ScriptTicket, 0), CancellationToken);
    await Task.Delay(TimeSpan.FromSeconds(1));

    var abandonResponse = await tentacleClient.ScriptServiceV2.AbandonScriptAsync(new AbandonScriptCommandV2(command.ScriptTicket, 0), CancellationToken);
    abandonResponse.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

    // mutex released check (same as Task 14)
    var secondCommand = new TestExecuteShellScriptCommandBuilder()
        .SetScriptBody(new ScriptBuilder().CreateFile(Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "second")))
        .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
        .WithIsolationMutexName("abandon-multilevel-mutex")
        .Build();
    var secondResult = await tentacleClient.ExecuteScript(secondCommand, CancellationToken);
    secondResult.response.ExitCode.Should().Be(0);

    File.WriteAllText(releaseFile, "");
}
```

- [ ] **Step 2: Run**

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --filter "AbandonScript_MultiLevelDeepHang"
```

Expected: passes.

- [ ] **Step 3: Commit**

```bash
git add source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs
git commit -m "Integration test: multi-level-deep hang abandons cleanly"
```

---

### Task 16: Full test suite + push for CI

- [ ] **Step 1: Run the entire test suite locally**

```bash
dotnet test source/Tentacle.sln
```

Expected: all tests pass on Linux. (Windows-only tests will skip locally if not on Windows.)

- [ ] **Step 2: Push for CI**

```bash
git push
```

Wait for the GitHub Actions check on PR #1226. All matrices (Linux, Windows, both target frameworks) must pass.

- [ ] **Step 3: Address any platform-specific failures**

Most likely areas:
- Workspace-cleanup test on Linux: Linux generally allows deletion of open files (the inode survives until handles close). The "delete fails" test may need a Windows-only attribute.
- Thread-count assertion timing: bump the delta tolerance if CI jitter is higher than dev box.

- [ ] **Step 4: Final commit (if any fixes needed)**

```bash
git add <fixed-files>
git commit -m "Address CI platform-specific test failures"
git push
```

---

## Self-review checklist (run after writing the plan, before handing off)

- [ ] Spec coverage: every section of `docs/superpowers/specs/2026-05-21-tentacle-script-abandon-design.md` maps to at least one task above.
- [ ] No `TODO`, `TBD`, `implement later`, or "add appropriate error handling" placeholders.
- [ ] Type/method names consistent across tasks (`ExecuteCommandAsync`, `AbandonScriptCommandV2`, `AbandonedExitCode`, `abandonToken`, `AbandonScriptAsync`).
- [ ] Every code step shows the actual code, not a description.
- [ ] Every command step shows the exact command and the expected outcome.

## Notes for execution

- **Frequent commits.** Each task above is one commit. Don't bundle.
- **Build green between tasks.** Task 3 introduces a `NotImplementedException` stub precisely so the build stays green between contracts and the implementation in Task 11.
- **Test cleanup.** Several integration tests leave a running PowerShell / bash sleep process behind (because `TentacleDebugDisableProcessKill` is set). The tests must release them via the release-file pattern. Forgetting cleanup will leak processes on the CI box.
- **Coordination with server-side.** Server-side session is on a parallel branch in `OctopusDeploy/OctopusDeploy`. Once both PRs are mergeable, coordinate the contract package version bump so Server picks up the new contract in lockstep.
