# Split Async Migration from Abandon Feature — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the existing PR stack so the async migration of `SilentProcessRunner` sits in its own foundational PR, and the abandon feature (PR #1226) stacks on top of it.

**Architecture:** End-state rebuild. Create a fresh branch from `main` containing only the async migration + sync-boundary comments. Then force-push #1226 with the abandon delta on top. PR #1235 rebases on the new #1226.

**Tech Stack:** C# (.NET 8 + net48 polyfill), Autofac DI, NUnit tests, git worktree workflow.

**Spec:** `docs/superpowers/specs/2026-05-25-split-async-migration-from-abandon-feature-design.md`

**Reference state:** The current tip of `jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex` is `583eb46c` (now: `46f09e7e` after the spec commit). The diff from `main` to that commit contains BOTH PRs' content combined.

---

## Phase 0 — Preparation

### Task 0.1: Tag the current state as a safety reference

**Files:** none (git only)

- [ ] **Step 1: Tag the current abandon branch tip**

```bash
cd /Users/jim/code/OctopusTentacle
git tag claude-safety-2026-05-25-pre-split jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex
git tag claude-safety-2026-05-25-pre-split-1235 jimpelletier/eft-3295-async-signature-propagation
```

- [ ] **Step 2: Verify tags**

```bash
git tag -l "claude-safety-*"
```

Expected: at least these two tags listed.

---

## Phase 1 — Build the base PR

### Task 1.1: Create new base branch from main

**Files:** none (git only)

- [ ] **Step 1: Fetch main**

```bash
cd /Users/jim/code/OctopusTentacle
git fetch origin main
```

- [ ] **Step 2: Create the new branch from origin/main**

```bash
git checkout -b jimpelletier/eft-3295-async-migration-base origin/main
```

- [ ] **Step 3: Verify**

```bash
git log --oneline -1
```

Expected: latest commit on `main`.

---

### Task 1.2: Migrate `SilentProcessRunner.ExecuteCommand` to async

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs`

The goal: change the method from sync to async with the MINIMUM internal changes. The `cancel` token is passed to `WaitForExitAsync(cancel)` so cancel still throws OCE and unwinds. `DoOurBestToCleanUp` remains unchanged — including the `process.Close()` call. `SafelyWaitForAllOutput` remains unchanged.

Use the `claude-safety-2026-05-25-pre-split` tag to see what the final state in `583eb46c` looks like, but ONLY take:
- The method signature change to `async Task<int> ExecuteCommandAsync(...)` (without `abandon` parameter)
- The internal `process.WaitForExit()` → `await process.WaitForExitAsync(cancel)` change
- The net48 polyfill `WaitForExitAsyncNetFramework`
- `process.EnableRaisingEvents = true` if it's needed for the polyfill

DO NOT take:
- The `abandon` parameter on the method
- Any changes to `DoOurBestToCleanUp` (keep `process.Close()` as it was on main)
- Any changes to `SafelyWaitForAllOutput` comments
- Any `OperationCanceledException when (abandon.IsCancellationRequested && !process.HasExited)` catch block

- [ ] **Step 1: Read the file on main**

```bash
git show origin/main:source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs > /tmp/srp_main.cs
git show claude-safety-2026-05-25-pre-split:source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs > /tmp/srp_target.cs
```

- [ ] **Step 2: Construct the base-PR version manually**

Use the main version as a starting point. Apply the minimum needed for async:
- Change `public static int ExecuteCommand(` → `public static async Task<int> ExecuteCommandAsync(`
- Add `using System.Threading.Tasks;`
- Inside the method, find `process.WaitForExit();` and change to:
  ```csharp
  #if NETFRAMEWORK
              await WaitForExitAsyncNetFramework(process, cancel).ConfigureAwait(false);
  #else
              await process.WaitForExitAsync(cancel).ConfigureAwait(false);
  #endif
  ```
- Set `process.EnableRaisingEvents = true;` before `process.Start();` (needed so the polyfill's `Process.Exited` event fires)
- Add the `WaitForExitAsyncNetFramework` polyfill at the end of the class, inside an `#if NETFRAMEWORK` block:
  ```csharp
  #if NETFRAMEWORK
          static Task WaitForExitAsyncNetFramework(Process process, CancellationToken cancellationToken)
          {
              var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
              CancellationTokenRegistration registration = default;

              void OnExited(object? sender, EventArgs e)
              {
                  registration.Dispose();
                  tcs.TrySetResult(null);
              }

              process.Exited += OnExited;
              if (process.HasExited)
              {
                  tcs.TrySetResult(null);
              }
              if (cancellationToken.CanBeCanceled)
              {
                  registration = cancellationToken.Register(() =>
                  {
                      process.Exited -= OnExited;
                      tcs.TrySetCanceled(cancellationToken);
                  });
              }
              return tcs.Task;
          }
  #endif
  ```

- [ ] **Step 3: Verify the file compiles standalone**

```bash
dotnet build source/Octopus.Tentacle.Core/Octopus.Tentacle.Core.csproj
```

Expected: build succeeds. Errors will likely be in callers, not this file.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs
git commit -m "$(cat <<'COMMIT'
Migrate SilentProcessRunner.ExecuteCommand to async

Replaces the sync WaitForExit() with await WaitForExitAsync(cancel).
The cancel token is passed directly so the existing cancel semantics
are preserved: cancel firing throws OCE from the await and unwinds.
DoOurBestToCleanUp continues to fire on cancel via cancel.Register
exactly as it did in the sync version.

Adds a net48 polyfill for WaitForExitAsync using Process.Exited +
TaskCompletionSource.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
COMMIT
)"
```

---

### Task 1.3: Migrate `ISilentProcessRunner` to async

**Files:**
- Modify: `source/Octopus.Tentacle/Util/ISilentProcessRunner.cs`

The interface defines the contract for `SilentProcessRunner.ExecuteCommand`. It will need an `ExecuteCommandAsync` method matching the new signature on the static `SilentProcessRunner` class.

- [ ] **Step 1: Read main version**

```bash
git show origin/main:source/Octopus.Tentacle/Util/ISilentProcessRunner.cs
```

- [ ] **Step 2: Read target version for reference**

```bash
git show claude-safety-2026-05-25-pre-split:source/Octopus.Tentacle/Util/ISilentProcessRunner.cs
```

- [ ] **Step 3: Construct the base-PR version**

Take the target version. Remove ANY `abandon` parameter. Change return type of methods from `int` to `Task<int>`. Add `CancellationToken cancel = default` if not already present.

Replace the `SilentProcessRunnerExtended` (or similar wrapper) implementation so it calls `SilentProcessRunner.ExecuteCommandAsync(...)` and awaits/returns the Task — NO `.GetAwaiter().GetResult()` inside.

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle/Util/ISilentProcessRunner.cs
git commit -m "$(cat <<'COMMIT'
Migrate ISilentProcessRunner to async

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
COMMIT
)"
```

---

### Task 1.4: Migrate `CommandLineRunner` and `CommandLineInvocation` to async

**Files:**
- Modify: `source/Octopus.Tentacle/Util/CommandLineRunner.cs`
- Modify: `source/Octopus.Tentacle.Core/Util/CommandLine/CommandLineInvocation.cs` (only if it has an Execute method)

`CommandLineRunner` wraps `SilentProcessRunner` and is consumed by Kubernetes integration tests and CLI helpers. Its `Execute` method becomes `ExecuteAsync`.

`CommandLineInvocation.ExecuteCommandAsync()` is referenced from `SystemCtlHelper`, `LinuxServiceConfigurator`, `WindowsServiceConfigurator`. If this method exists on `CommandLineInvocation`, migrate it to async (no `abandon` param).

- [ ] **Step 1: Check whether CommandLineInvocation has an Execute method**

```bash
grep -n "Execute" source/Octopus.Tentacle.Core/Util/CommandLine/CommandLineInvocation.cs 2>/dev/null || echo "no Execute method in CommandLineInvocation"
```

- [ ] **Step 2: Read both versions**

```bash
git show origin/main:source/Octopus.Tentacle/Util/CommandLineRunner.cs
git show claude-safety-2026-05-25-pre-split:source/Octopus.Tentacle/Util/CommandLineRunner.cs
```

- [ ] **Step 3: Construct the base-PR version**

Make `CommandLineRunner.Execute` → no, keep `Execute` (the existing public method is sync and consumed by the WPF installer, which must remain sync). Inside `Execute`, where it calls the underlying process runner: it currently does so via `.GetAwaiter().GetResult()`. KEEP that. The improved comment goes on the GetAwaiter line:

```csharp
// We're in CommandLineRunner.Execute, called from the WPF installer (Octopus.Manager.Tentacle)
// running on a thread-pool worker after the installer hands off to our process-runner helper.
// CommandLineRunner.Execute itself must return synchronously because the installer's UI flow
// is sync. We block on the async call with .GetAwaiter().GetResult().
// This is safe because we're on a plain thread-pool worker. The risk with blocking on async
// is a deadlock: if the async work needs to resume on the same thread that's blocked waiting
// for it, neither can make progress. Thread-pool workers don't have that constraint — the
// async work can pick up on any free thread when it finishes, so the block resolves normally.
var exitCode = SilentProcessRunner.ExecuteCommandAsync(/* args */).GetAwaiter().GetResult();
```

- [ ] **Step 4: Commit**

```bash
git add source/Octopus.Tentacle/Util/CommandLineRunner.cs source/Octopus.Tentacle.Core/Util/CommandLine/CommandLineInvocation.cs 2>/dev/null
git commit -m "Migrate CommandLineRunner and CommandLineInvocation to async

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 1.5: Migrate `RunningScript` to async (no abandon token)

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs`

`RunningScript.RunScript()` calls `SilentProcessRunner.ExecuteCommand`. Now that ExecuteCommand is async, RunScript must also become async. RunningScript stays WITHOUT the abandon token in the base PR.

- [ ] **Step 1: Read both versions for reference**

```bash
git show origin/main:source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs | head -100
git show claude-safety-2026-05-25-pre-split:source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs | head -100
```

- [ ] **Step 2: Build the base version**

Take the target version and remove:
- `CancellationToken abandonToken` constructor parameter
- `abandonToken` field
- `abandon: abandonToken` argument when calling `ExecuteCommandAsync`
- Any `OperationCanceledException when (abandonToken.IsCancellationRequested)` catch branches
- Any references to `AbandonedExitCode` (those aren't in `ScriptExitCodes` yet)

Make the public method async: `RunScript` → `RunScriptAsync` returning `Task<int>`.

- [ ] **Step 3: Commit**

```bash
git add source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs
git commit -m "Migrate RunningScript to async

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 1.6: Migrate `ScriptServiceV2` callsite to async (no abandon)

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs`

`ScriptServiceV2.StartScriptAsync` calls into `RunningScript.RunScript`. Update to await `RunScriptAsync`. Do NOT add `AbandonScriptAsync` here yet.

- [ ] **Step 1: Read both versions**

```bash
git show origin/main:source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs > /tmp/scs_main.cs
git show claude-safety-2026-05-25-pre-split:source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs > /tmp/scs_target.cs
```

- [ ] **Step 2: Build the base version**

Take the main version and apply ONLY the minimal changes needed to await the new async `RunScriptAsync` from `RunningScript`. Remove all abandon-specific additions in the target version:
- No `AbandonScriptAsync` method
- No `RunningScriptWrapper.AbandonTokenSource` / `Abandon()` method
- No `AbandonedExitCode` references
- No abandon-specific workspace deletion logic

- [ ] **Step 3: Commit**

```bash
git add source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs
git commit -m "Update ScriptServiceV2 to await async RunScriptAsync

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 1.7: Update the six sync↔async boundary sites with improved comments

**Files:**
- Modify: `source/Octopus.Manager.Tentacle/PreReq/PowerShellPrerequisite.cs`
- Modify: `source/Octopus.Tentacle/Kubernetes/KubernetesDirectoryInformationProvider.cs`
- Modify: `source/Octopus.Tentacle/Util/SystemCtlHelper.cs`
- Modify: `source/Octopus.Tentacle/Startup/LinuxServiceConfigurator.cs`
- Modify: `source/Octopus.Tentacle/Startup/WindowsServiceConfigurator.cs`

Each of these sites was the immediate consumer of `ExecuteCommand` in main. After Task 1.2 they need to call `ExecuteCommandAsync` and either await it (if they can become async) or block with `.GetAwaiter().GetResult()` (if they cannot).

ALL of these sites CANNOT become async in this PR — they implement sync interfaces (IPrerequisite, IMemoryCache factory, IServiceConfigurator) or are called from sync framework code (Topshelf). They all use `.GetAwaiter().GetResult()` with an explanatory comment.

Comment template (adapt the specifics per site):

```
// We're in [SHORT DESCRIPTION OF SITE]. [WHY IT MUST BE SYNC — interface
// constraint, framework callback, etc.]. We block on the async call with
// .GetAwaiter().GetResult().
// This is safe because we're on a plain thread-pool worker. The risk with
// blocking on async is a deadlock: if the async work needs to resume on
// the same thread that's blocked waiting for it, neither can make progress.
// Thread-pool workers don't have that constraint — the async work can
// pick up on any free thread when it finishes, so the block resolves normally.
```

- [ ] **Step 1: Update PowerShellPrerequisite**

Site: `source/Octopus.Manager.Tentacle/PreReq/PowerShellPrerequisite.cs`. The `Check()` method calls `SilentProcessRunner.ExecuteCommandAsync(...).GetAwaiter().GetResult()`. Comment specifics: "We're in the WPF installer prerequisite check. IPrerequisite.Check() must return synchronously — there's no async version of the interface — so we block..."

Reference state: `git show claude-safety-2026-05-25-pre-split:source/Octopus.Manager.Tentacle/PreReq/PowerShellPrerequisite.cs`

Copy that file's content directly — it has the right comment already.

- [ ] **Step 2: Update KubernetesDirectoryInformationProvider**

Site: `source/Octopus.Tentacle/Kubernetes/KubernetesDirectoryInformationProvider.cs`. Method `GetDriveBytesUsingDu` is called from inside an `IMemoryCache.GetOrCreate` factory (a `Func<ICacheEntry, T>` — sync). Comment specifics: "We're in the IMemoryCache.GetOrCreate factory that populates the disk-space cache entry. The cache factory delegate is synchronous (`Func<ICacheEntry, T>`) so we block on the async call with `.GetAwaiter().GetResult()`..."

Take this content from the safety tag, BUT verify it does not include any async chain propagation (it shouldn't — we never propagated this in the abandon PR). It should be `GetPathUsedBytes` (sync) with GetAwaiter on the du call.

```bash
git show claude-safety-2026-05-25-pre-split:source/Octopus.Tentacle/Kubernetes/KubernetesDirectoryInformationProvider.cs
```

If the file at safety tag has `GetPathUsedBytesAsync` or other async-chain content, that came from PR #1235 work that was rolled back. Use the file with the sync `GetPathUsedBytes` + GetAwaiter pattern.

- [ ] **Step 3: Update SystemCtlHelper**

Site: `source/Octopus.Tentacle/Util/SystemCtlHelper.cs`. Two GetAwaiter calls inside `RunServiceCommand` (one for systemctl, one for sudo retry). Comment specifics: "We're in SystemCtlHelper running a systemctl command. All callers (StartService, RestartService, etc.) are sync — they're part of the Tentacle service-management CLI flow, which bottoms out in ServiceCommand.Start() (sync `void` override) with no async path..."

Second GetAwaiter call (sudo retry) gets a short pointer comment: "Same sync boundary — sudo retry on the same thread-pool worker."

- [ ] **Step 4: Update LinuxServiceConfigurator**

Site: `source/Octopus.Tentacle/Startup/LinuxServiceConfigurator.cs`. Three GetAwaiter calls: `WriteUnitFile`, `IsSystemdInstalled`, `HaveSudoPrivileges`. Each gets the comment template, adapted:

For `WriteUnitFile`: "WriteUnitFile is called from `IServiceConfigurator.ConfigureService` implementations, which are themselves called from the Tentacle service-management CLI on a thread-pool worker..."

For `IsSystemdInstalled` and `HaveSudoPrivileges`: "Same sync boundary as WriteUnitFile."

- [ ] **Step 5: Update WindowsServiceConfigurator**

Site: `source/Octopus.Tentacle/Startup/WindowsServiceConfigurator.cs`. One GetAwaiter call inside `Sc()`. Comment specifics: "Sc() is called from `IServiceConfigurator.ConfigureService` implementations on Windows, on a thread-pool worker..."

- [ ] **Step 6: Commit**

```bash
git add source/Octopus.Manager.Tentacle/PreReq/PowerShellPrerequisite.cs \
        source/Octopus.Tentacle/Kubernetes/KubernetesDirectoryInformationProvider.cs \
        source/Octopus.Tentacle/Util/SystemCtlHelper.cs \
        source/Octopus.Tentacle/Startup/LinuxServiceConfigurator.cs \
        source/Octopus.Tentacle/Startup/WindowsServiceConfigurator.cs
git commit -m "$(cat <<'COMMIT'
Document the six sync↔async boundary sites with improved comments

Each immediate sync caller of ExecuteCommandAsync now blocks via
.GetAwaiter().GetResult() with a comment that explains where it sits
in the call graph, why the surrounding code must be synchronous, and
why blocking on async is deadlock-safe from a plain thread-pool worker.

Sites:
- PowerShellPrerequisite.Check (WPF installer prerequisite)
- KubernetesDirectoryInformationProvider.GetDriveBytesUsingDu (IMemoryCache factory)
- SystemCtlHelper.RunServiceCommand (×2 — systemctl + sudo retry)
- LinuxServiceConfigurator: WriteUnitFile, IsSystemdInstalled, HaveSudoPrivileges
- WindowsServiceConfigurator.Sc

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
COMMIT
)"
```

---

### Task 1.8: Update other test scaffolding files

**Files:**
- The Kubernetes integration test files listed in the diff (TestUtils, Setup, Tooling, etc.) likely need to be migrated to async because they consume `CommandLineRunner` or `SilentProcessRunner`.

The diff from `main` to `583eb46c` lists these:
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/*.cs`
- `source/Octopus.Tentacle.Kubernetes.Tests.Integration/Tooling/*.cs`
- `source/Octopus.Tentacle.Tests.Integration/Support/*.cs`
- `source/Octopus.Tentacle.Tests.Integration/Util/*.cs`
- `source/Octopus.Tentacle.Tests/Util/LinuxTestUserPrincipal.cs`

Most of these only changed because they had to switch from sync `Execute` to async `ExecuteAsync`. Copy the safety-tag versions BUT verify each one only contains async-migration changes (no abandon-related changes). If a file contains abandon test fixtures, take only the async portions.

- [ ] **Step 1: For each file, compare main vs safety tag**

```bash
for f in \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/SetupHelpers.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesClusterInstaller.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/KubernetesAgentInstaller.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/DockerImageLoader.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/HelmDownloader.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/Setup/Tooling/ToolDownloader.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/Tooling/KubeCtlTool.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/KubernetesAgent/KubernetesClusterOneTimeSetUp.cs \
  source/Octopus.Tentacle.Kubernetes.Tests.Integration/KubernetesClientCompatibilityTests.cs \
  source/Octopus.Tentacle.Tests.Integration/Support/ClientAndTentacle.cs \
  source/Octopus.Tentacle.Tests.Integration/Support/TentacleFetchers/LinuxTentacleFetcher.cs \
  source/Octopus.Tentacle.Tests.Integration/Util/LinuxTestUserPrincipal.cs \
  source/Octopus.Tentacle.Tests.Integration/Util/RunningScriptFixture.cs \
  source/Octopus.Tentacle.Tests/Util/LinuxTestUserPrincipal.cs ; do
  echo "=== $f ==="
  git diff origin/main..claude-safety-2026-05-25-pre-split -- "$f" | head -20
  echo
done
```

- [ ] **Step 2: For each file, take the safety-tag version IF its changes are purely async-migration**

Use `git checkout claude-safety-2026-05-25-pre-split -- <file>` for each.

If a file contains abandon-specific additions (e.g., references to `AbandonScript` or `AbandonedExitCode`), manually edit out those parts after checkout.

- [ ] **Step 3: Build**

```bash
dotnet build source/Octopus.Tentacle.sln
```

Expected: build succeeds. Errors here will reveal additional files that need attention.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "$(cat <<'COMMIT'
Migrate test scaffolding to async ExecuteCommandAsync

Updates Kubernetes integration test setup and support helpers to await
the new ExecuteCommandAsync signature. No abandon-feature content is
included.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
COMMIT
)"
```

---

### Task 1.9: Build verification — base PR must compile and tests must pass

**Files:** none (verification only)

- [ ] **Step 1: Full build**

```bash
cd /Users/jim/code/OctopusTentacle/source
dotnet build Octopus.Tentacle.sln 2>&1 | tail -50
```

Expected: 0 errors. Any errors must be resolved before proceeding — they indicate missing files in the migration.

- [ ] **Step 2: Run the unit tests**

```bash
dotnet test source/Octopus.Tentacle.Tests/Octopus.Tentacle.Tests.csproj
```

Expected: all green.

- [ ] **Step 3: Run the SilentProcessRunner integration test for ShouldCancelPing**

```bash
dotnet test source/Octopus.Tentacle.Tests.Integration --filter "Name~ShouldCancelPing"
```

Expected: green. This verifies cancel works with our `WaitForExitAsync(cancel)` wiring.

---

### Task 1.10: Push the base branch and open the new PR

**Files:** none (git + gh)

- [ ] **Step 1: Push**

```bash
git push -u origin jimpelletier/eft-3295-async-migration-base
```

- [ ] **Step 2: Create the PR with base = main**

```bash
gh pr create \
  --base main \
  --head jimpelletier/eft-3295-async-migration-base \
  --title "Migrate SilentProcessRunner to async" \
  --body "$(cat <<'EOF'
## Summary

Makes `SilentProcessRunner.ExecuteCommand` async. Required foundation for the EFT-3295 script-abandonment feature (PR #1226, which stacks on top of this) but valuable on its own as a refactor: enables awaiting process runs from already-async callers rather than blocking a thread.

### What this PR does
- `SilentProcessRunner.ExecuteCommand` → `ExecuteCommandAsync` (and the matching interfaces and helpers)
- Internal: `process.WaitForExit()` → `await process.WaitForExitAsync(cancel)`
- Adds a net48 polyfill for `WaitForExitAsync` (using `Process.Exited` + `TaskCompletionSource`)
- The six immediate sync callers (PowerShellPrerequisite, KubernetesDirectoryInformationProvider, SystemCtlHelper×2, LinuxServiceConfigurator×3, WindowsServiceConfigurator) block via `.GetAwaiter().GetResult()` with a comment explaining the call context and why blocking on a thread-pool worker is deadlock-safe

### What this PR explicitly does NOT include
- The `abandon` parameter on `ExecuteCommandAsync` (added in #1226)
- Removal of `process.Close()` from `DoOurBestToCleanUp` (added in #1226)
- Any abandon-specific contracts, RPC methods, capabilities, env vars, or tests (#1226)

## Test plan
- [ ] CI build green
- [ ] `ShouldCancelPing` integration test still passes (cancel semantics preserved)

🤖 Generated with [Claude Code](https://claude.ai/claude-code)
EOF
)"
```

- [ ] **Step 3: Capture the new PR number for use in subsequent tasks**

```bash
gh pr view jimpelletier/eft-3295-async-migration-base --json number,url
```

---

## Phase 2 — Rebuild #1226 on top of the base PR

### Task 2.1: Reset the abandon branch to the base PR tip

**Files:** none (git only)

- [ ] **Step 1: Switch to the abandon branch**

```bash
cd /Users/jim/code/OctopusTentacle
git checkout jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex
```

- [ ] **Step 2: Hard-reset to the new base branch tip**

```bash
git reset --hard jimpelletier/eft-3295-async-migration-base
```

- [ ] **Step 3: Verify**

```bash
git log --oneline -1
```

Expected: tip of the base branch.

---

### Task 2.2: Apply the abandon delta — contracts, env var

**Files:**
- Create: `source/Octopus.Tentacle.Contracts/ScriptServiceV2/AbandonScriptCommandV2.cs`
- Modify: `source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs` (add `AbandonedExitCode = -48`)
- Modify: `source/Octopus.Tentacle.Contracts/ScriptServiceV2/IScriptServiceV2.cs` (add `AbandonScript` method)
- Modify: `source/Octopus.Tentacle.Contracts/ClientServices/IAsyncClientScriptServiceV2.cs` (add `AbandonScriptAsync`)
- Modify: `source/Octopus.Tentacle.Core/Util/EnvironmentVariables.cs` (add `TentacleDebugDisableProcessKill`)

- [ ] **Step 1: Copy each file from safety tag**

```bash
git checkout claude-safety-2026-05-25-pre-split -- \
  source/Octopus.Tentacle.Contracts/ScriptServiceV2/AbandonScriptCommandV2.cs \
  source/Octopus.Tentacle.Contracts/ScriptExitCodes.cs \
  source/Octopus.Tentacle.Contracts/ScriptServiceV2/IScriptServiceV2.cs \
  source/Octopus.Tentacle.Contracts/ClientServices/IAsyncClientScriptServiceV2.cs \
  source/Octopus.Tentacle.Core/Util/EnvironmentVariables.cs
```

- [ ] **Step 2: Commit**

```bash
git commit -am "Add abandon contracts and TentacleDebugDisableProcessKill env var

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.3: Apply the abandon delta — SilentProcessRunner abandon token + Close removal + long-form comments

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs`

This step:
- Adds the `abandon` parameter to `ExecuteCommandAsync`
- Switches internal await from `WaitForExitAsync(cancel)` to `WaitForExitAsync(abandon)`
- Adds the `OperationCanceledException when (abandon.IsCancellationRequested && !process.HasExited)` catch returning `ScriptExitCodes.AbandonedExitCode`
- Removes `process.Close()` from `DoOurBestToCleanUp`
- Adds long-form documentation comments to `DoOurBestToCleanUp`, `SafelyWaitForAllOutput`, and the `WaitForExitAsync` call site
- Adds the Hitman env-var test-affordance check

- [ ] **Step 1: Take the safety-tag version of SilentProcessRunner**

```bash
git checkout claude-safety-2026-05-25-pre-split -- source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs
```

- [ ] **Step 2: Verify the stray `process.Close()` bug fix is included**

```bash
grep -n "process.Close" source/Octopus.Tentacle.Core/Util/CommandLine/SilentProcessRunner.cs
```

Expected output: only references in comments (no actual `process.Close();` call). If a `process.Close();` call appears not in a comment, remove it manually — same fix as in commit `583eb46c`.

- [ ] **Step 3: Commit**

```bash
git commit -am "Add abandon token to SilentProcessRunner and remove process.Close() race

- Adds CancellationToken abandon parameter to ExecuteCommandAsync
- Switches the await from WaitForExitAsync(cancel) to WaitForExitAsync(abandon)
- Returns ScriptExitCodes.AbandonedExitCode when abandon fires before process exits
- Removes process.Close() from DoOurBestToCleanUp (race with WaitForExitAsync's
  TCS via the Exited event — Close tore down the wait state, hung cancel)
- Adds long-form documentation comments explaining the race, the grandchild-pipe
  scenario, and worst-case cancel latency
- Adds TentacleDebugDisableProcessKill test affordance to Hitman

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.4: Apply the abandon delta — interface + caller updates for abandon parameter

**Files:**
- Modify: `source/Octopus.Tentacle/Util/ISilentProcessRunner.cs`
- Modify: `source/Octopus.Tentacle/Util/CommandLineRunner.cs`
- Modify: `source/Octopus.Tentacle.Core/Util/CommandLine/CommandLineInvocation.cs` (if it has an Execute method)

Add the `abandon` parameter to the interface and the helper class.

- [ ] **Step 1: Take the safety-tag versions**

```bash
git checkout claude-safety-2026-05-25-pre-split -- \
  source/Octopus.Tentacle/Util/ISilentProcessRunner.cs \
  source/Octopus.Tentacle/Util/CommandLineRunner.cs
git checkout claude-safety-2026-05-25-pre-split -- source/Octopus.Tentacle.Core/Util/CommandLine/CommandLineInvocation.cs 2>/dev/null
```

- [ ] **Step 2: Commit**

```bash
git commit -am "Plumb abandon token through ISilentProcessRunner and CommandLineRunner

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.5: Apply the abandon delta — RunningScript abandon-token plumbing

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs`

Adds the `abandonToken` constructor parameter and passes it to `ExecuteCommandAsync`.

- [ ] **Step 1: Take the safety-tag version**

```bash
git checkout claude-safety-2026-05-25-pre-split -- source/Octopus.Tentacle.Core/Services/Scripts/RunningScript.cs
```

- [ ] **Step 2: Commit**

```bash
git commit -am "Plumb abandon token through RunningScript

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.6: Apply the abandon delta — ScriptServiceV2.AbandonScriptAsync + workspace cleanup

**Files:**
- Modify: `source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs`

Adds:
- `RunningScriptWrapper.AbandonTokenSource` and `Abandon()`
- Public `AbandonScriptAsync` method on the service
- Best-effort `workspace.Delete` gated on `AbandonedExitCode`

- [ ] **Step 1: Take the safety-tag version**

```bash
git checkout claude-safety-2026-05-25-pre-split -- source/Octopus.Tentacle.Core/Services/Scripts/ScriptServiceV2.cs
```

- [ ] **Step 2: Commit**

```bash
git commit -am "Implement ScriptServiceV2.AbandonScriptAsync and abandon-gated workspace cleanup

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.7: Apply the abandon delta — advertise AbandonScriptV2 capability

**Files:**
- Modify: `source/Octopus.Tentacle/Services/Capabilities/CapabilitiesServiceV2.cs`
- Modify: `source/Octopus.Tentacle.Tests.Integration/CapabilitiesServiceV2Test.cs`

Adds `nameof(IAsyncClientScriptServiceV2.AbandonScriptAsync)` to the capabilities list. Updates the integration test to expect it for Latest tentacles.

- [ ] **Step 1: Take the safety-tag versions**

```bash
git checkout claude-safety-2026-05-25-pre-split -- \
  source/Octopus.Tentacle/Services/Capabilities/CapabilitiesServiceV2.cs \
  source/Octopus.Tentacle.Tests.Integration/CapabilitiesServiceV2Test.cs
```

- [ ] **Step 2: Commit**

```bash
git commit -am "Advertise AbandonScriptV2 capability

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.8: Apply the abandon delta — ScriptBuilder.AppendRaw, tests, and grandchild test comments

**Files:**
- Modify: `source/Octopus.Tentacle.CommonTestUtils/Builders/ScriptBuilder.cs`
- Modify: `source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs`
- Create or modify: `source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs`
- Modify: `source/Octopus.Tentacle.Tests/Integration/ScriptServiceV2Fixture.cs`
- Modify: `source/Octopus.Tentacle.Tests.Integration.Common/Builders/Decorators/ScriptServiceV2DecoratorBuilder.cs`
- Modify: `source/Octopus.Tentacle.Tests/Kubernetes/KubernetesDirectoryInformationProviderFixture.cs` (if it has abandon-specific test changes)

The abandon-specific tests and test helpers. Includes the rewritten grandchild test comments in `SilentProcessRunnerFixture`.

- [ ] **Step 1: Take the safety-tag versions**

```bash
git checkout claude-safety-2026-05-25-pre-split -- \
  source/Octopus.Tentacle.CommonTestUtils/Builders/ScriptBuilder.cs \
  source/Octopus.Tentacle.Tests.Integration/Util/SilentProcessRunnerFixture.cs \
  source/Octopus.Tentacle.Tests.Integration/ClientScriptExecutionAbandon.cs \
  source/Octopus.Tentacle.Tests/Integration/ScriptServiceV2Fixture.cs \
  source/Octopus.Tentacle.Tests.Integration.Common/Builders/Decorators/ScriptServiceV2DecoratorBuilder.cs
```

- [ ] **Step 2: Commit**

```bash
git commit -am "Add abandon-specific tests and rewrite grandchild test comments for async behavior

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.9: Apply remaining files — spec doc, plan doc, anything else in diff

**Files:**
- Spec/plan files from `docs/superpowers/`
- Any remaining file in the `git diff main..claude-safety-2026-05-25-pre-split` that's not already covered

- [ ] **Step 1: List files still differing**

```bash
git diff jimpelletier/eft-3295-async-migration-base..claude-safety-2026-05-25-pre-split --name-only
```

- [ ] **Step 2: Inspect any unhandled files and bring them over**

For each remaining file:
- If the change is abandon-specific: `git checkout claude-safety-2026-05-25-pre-split -- <file>`
- If unrelated: skip and ask user

- [ ] **Step 3: Verify the diff is complete**

```bash
git diff jimpelletier/eft-3295-async-migration-base..HEAD --stat
```

This should now contain the FULL abandon-feature delta.

- [ ] **Step 4: Verify end state matches the safety tag**

```bash
git diff claude-safety-2026-05-25-pre-split HEAD
```

Expected: zero output. The rebuilt branch should produce the EXACT same end state as `583eb46c`.

If there are differences, investigate and resolve them before continuing.

- [ ] **Step 5: Commit any final additions**

```bash
git status
git add -A
git commit -m "Bring in remaining abandon-feature files

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>" || echo "no changes"
```

---

### Task 2.10: Build verification — abandon PR must compile and all tests must pass

**Files:** none (verification only)

- [ ] **Step 1: Full build**

```bash
dotnet build source/Octopus.Tentacle.sln 2>&1 | tail -50
```

Expected: 0 errors.

- [ ] **Step 2: Run abandon-specific tests**

```bash
dotnet test source/Octopus.Tentacle.Tests/Octopus.Tentacle.Tests.csproj --filter "Name~Abandon"
dotnet test source/Octopus.Tentacle.Tests.Integration --filter "Name~Abandon"
```

Expected: green.

---

### Task 2.11: Force-push abandon branch and update PR #1226's base

**Files:** none (git + gh)

- [ ] **Step 1: Force-push**

```bash
git push --force-with-lease origin jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex
```

- [ ] **Step 2: Change PR #1226's base to the new async-migration-base branch**

```bash
gh pr edit 1226 --base jimpelletier/eft-3295-async-migration-base
```

- [ ] **Step 3: Add a comment to #1226 explaining the rebase**

```bash
gh pr comment 1226 --body "$(cat <<'EOF'
Rebased on top of the new foundational PR (the async migration of \`SilentProcessRunner\`). The diff is now focused on the abandon feature itself — the async-migration plumbing has moved to the base PR.

Previous head: \`583eb46c\` (preserved as tag \`claude-safety-2026-05-25-pre-split\`).
EOF
)"
```

---

## Phase 3 — Rebase PR #1235

### Task 3.1: Rebase #1235 on top of the new #1226

**Files:** none (git)

- [ ] **Step 1: Switch to #1235's branch**

```bash
cd /Users/jim/code/OctopusTentacle
git checkout jimpelletier/eft-3295-async-signature-propagation
```

- [ ] **Step 2: Rebase onto the new #1226 tip**

```bash
git rebase jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex
```

If conflicts arise: resolve each one. The most likely conflict file is `SilentProcessRunner.cs` (because #1235 had the stray `process.Close()` fix that's now in #1226). Other conflicts are mechanical — resolve in favor of the #1235 version since those are the push-higher changes.

- [ ] **Step 3: Verify build**

```bash
dotnet build source/Octopus.Tentacle.sln 2>&1 | tail -20
```

- [ ] **Step 4: Force-push #1235**

```bash
git push --force-with-lease origin jimpelletier/eft-3295-async-signature-propagation
```

- [ ] **Step 5: Sanity check #1235's PR diff**

```bash
gh pr view 1235 --json url
```

Visit the URL and confirm the diff contains only the push-higher commits (no abandon-feature content leaked).

---

## Phase 4 — Final verification

### Task 4.1: End-to-end stack check

**Files:** none

- [ ] **Step 1: Verify branch graph**

```bash
git log --oneline --graph --all -30
```

Expected: `main` → base branch → abandon branch → push-higher branch.

- [ ] **Step 2: Verify each PR's base**

```bash
gh pr list --head jimpelletier/eft-3295-async-migration-base
gh pr list --head jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex
gh pr list --head jimpelletier/eft-3295-async-signature-propagation
```

Expected:
- New base PR → base: `main`
- #1226 → base: `jimpelletier/eft-3295-async-migration-base`
- #1235 → base: `jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex`

- [ ] **Step 3: Verify end-state equivalence**

```bash
# When all three PRs are squash-merged, the result on main should equal the safety tag's file states
git diff claude-safety-2026-05-25-pre-split jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex
```

Expected: zero output (the rebased #1226 ends at the same end-state as the original tip).

```bash
git diff claude-safety-2026-05-25-pre-split-1235 jimpelletier/eft-3295-async-signature-propagation
```

Expected: zero output (the rebased #1235 ends at the same end-state as before).

- [ ] **Step 4: Report success**

Report each PR's URL and confirm the inversion is complete.

---

## Open notes for the implementer

- If `git checkout claude-safety-2026-05-25-pre-split -- <file>` brings over content that includes abandon-specific changes when a file is supposed to be "async-migration only," check whether the file at safety-tag has BOTH concerns mixed. If so, you'll need to manually edit out the abandon parts. This is most likely for: `SilentProcessRunner.cs`, `RunningScript.cs`, `ScriptServiceV2.cs`, `ISilentProcessRunner.cs`, `CommandLineRunner.cs`.
- If a build error during Phase 1 says a method has the wrong signature, it likely means the abandon-token parameter leaked into the base-PR version of an interface. Search for `abandon` in the file and remove.
- The `.worktrees/` directory is gitignored from the abandon branch but NOT from `main`. If `git status` shows it as untracked on the base branch, that's expected — the gitignore was added in the abandon branch only. The base PR should NOT include this gitignore change (it's not async-migration related).
