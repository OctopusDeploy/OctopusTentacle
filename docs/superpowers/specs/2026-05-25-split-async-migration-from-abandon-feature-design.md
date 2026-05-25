# Split async migration into its own PR beneath the abandon feature

## Context

Current state:

- **PR #1226 (`jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex`)** at commit `583eb46c` contains both:
  - The async migration of `SilentProcessRunner.ExecuteCommand` → `ExecuteCommandAsync` (and all its callers).
  - The script-abandonment feature (abandon token, `AbandonScriptCommandV2`, `AbandonedExitCode`, RPC method, capability, tests).
- **PR #1235 (`jimpelletier/eft-3295-async-signature-propagation`)** stacks on top of #1226 and pushes the async signature higher into CLI host, Kubernetes paths, `IServiceConfigurator`, `ICommandLineRunner`, etc.

The stack is currently:

```
main ← #1226 (abandon + async migration) ← #1235 (push higher)
```

## Goal

Invert the lower half of the stack so the async migration sits beneath the abandon feature:

```
main ← [NEW BASE PR] async migration ← #1226 (rebased: abandon feature only) ← #1235 (push higher, unchanged in shape)
```

The new base PR is a clean refactoring change that is reviewable and mergeable independently of the abandon feature. #1226 becomes a focused feature PR that adds script abandonment on top of the foundation.

## Non-goals

- This spec does NOT cover restructuring #1235. That PR's content remains as it is and continues to stack on top of #1226.
- This spec does NOT widen the scope of the abandon feature. The abandon feature's existing content is preserved, just rebased.
- This spec does NOT attempt to surgically split the existing #1226 commits via cherry-pick or interactive rebase. The commits intermix concerns and would conflict heavily.

## Approach

End-state rebuild rather than commit surgery.

Take the file states at `583eb46c` and split them into two clean sets of changes built from `main`. Each PR is constructed as a small number of logical commits that produce the same final state when stacked.

### Base PR — "Migrate SilentProcessRunner to async"

Branch name: `jimpelletier/eft-3295-async-migration-base`.

Scope: the minimum change required to make `SilentProcessRunner.ExecuteCommand` async, with documented sync↔async boundaries at every immediate caller.

Contents:

1. **`SilentProcessRunner`** — `ExecuteCommand` → `ExecuteCommandAsync`. Internal change: `process.WaitForExit()` → `await process.WaitForExitAsync(cancel)`. The `cancel` token is passed directly to `WaitForExitAsync` so the existing cancel semantics are preserved (when cancel fires, the await throws `OperationCanceledException`; the existing `cancel.Register(() => DoOurBestToCleanUp(...))` still fires Kill+Close on a separate thread). **No other SilentProcessRunner changes.** `DoOurBestToCleanUp` remains unchanged including the `process.Close()` call. `SafelyWaitForAllOutput` remains unchanged.
2. **NET Framework polyfill** for `WaitForExitAsync` (not available on net48): a `WaitForExitAsyncNetFramework` helper using `Process.Exited` event + `TaskCompletionSource`.
3. **`ISilentProcessRunner`, `CommandLineRunner`, `CommandLineInvocation`** — interface and helper class signatures migrated to async.
4. **Immediate sync callers** — six sites updated with `.GetAwaiter().GetResult()`:
   - `PowerShellPrerequisite.Check()` (WPF installer prerequisite)
   - `KubernetesDirectoryInformationProvider.GetDriveBytesUsingDu()` (called from `IMemoryCache.GetOrCreate` factory)
   - `SystemCtlHelper.RunServiceCommand()` (2 call sites)
   - `LinuxServiceConfigurator.WriteUnitFile`, `IsSystemdInstalled`, `HaveSudoPrivileges` (3 call sites)
   - `WindowsServiceConfigurator.Sc()`
   - `CommandLineRunner.Execute(CommandLineInvocation, ...)`
5. **Sync-boundary comments** — every one of the six sites gets the same comment pattern: "We're in X. Y must be sync because Z. We block with `.GetAwaiter().GetResult()`. This is safe because we're on a plain thread-pool worker — when the async work finishes it can resume on any free thread, so the block resolves normally."
6. **`CapabilitiesServiceV2` nameof change** — replace the `"AbandonScriptV2"` string literal with `nameof(...)`. Small refactor that fits the cleanup theme.

What this PR does NOT include:

- No `abandon` parameter on `ExecuteCommandAsync`.
- No removal of `process.Close()` from `DoOurBestToCleanUp`.
- No long-form documentation comments on `DoOurBestToCleanUp`, `SafelyWaitForAllOutput`, or the `WaitForExitAsync` call site (those describe race-related semantics that only matter once the abandon flow is added).
- No grandchild test comment improvements (those describe the async-cancel race the abandon PR fixes).
- No abandon-specific contracts, RPC methods, capabilities, env vars, or tests.

### Stacked PR — "Add Script abandonment feature" (rebased #1226)

Branch: `jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex` (force-pushed).

Scope: the script abandonment feature, building on the async foundation.

Contents:

1. **Add `abandon` parameter to `ExecuteCommandAsync`** — second `CancellationToken` parameter. Switch internal await from `WaitForExitAsync(cancel)` to `WaitForExitAsync(abandon)`. Cancel continues to flow through `cancel.Register`.
2. **Remove `process.Close()` from `DoOurBestToCleanUp`** — cancel-path race fix. Now needed because the abandon flow relies on `Exited`-event delivery via `WaitForExitAsync(abandon)`'s TCS, and `Close()` tears down the wait state.
3. **Long-form documentation comments** on `DoOurBestToCleanUp`, `SafelyWaitForAllOutput`, and the `WaitForExitAsync` call site explaining the race, the grandchild-pipe scenario, and the worst-case cancel latency.
4. **Contracts** — `AbandonedExitCode = -48`, `AbandonScriptCommandV2`, `IScriptServiceV2.AbandonScript`, `IAsyncClientScriptServiceV2.AbandonScriptAsync`.
5. **`ScriptServiceV2.AbandonScriptAsync` implementation** — abandon-token wrapper, fires abandon CTS, returns response.
6. **Abandon-token plumbing through `RunningScript`** — constructor accepts abandon token, passes through to `ExecuteCommandAsync`.
7. **`TentacleDebugDisableProcessKill` env var** — test affordance for the stuck-script scenario.
8. **`AbandonScriptV2` capability** — advertised in `CapabilitiesServiceV2`.
9. **Best-effort `workspace.Delete`** gated on `AbandonedExitCode` in `CompleteScriptAsync`.
10. **Abandon-specific tests** — service-layer (`ScriptServiceV2Fixture`) and integration (`ClientScriptExecutionAbandon`, `SilentProcessRunnerFixture.AbandonToken_*`).
11. **Improved grandchild test comments** — rewritten to describe the async behavior being guarded.

### PR #1235 unchanged

`jimpelletier/eft-3295-async-signature-propagation` continues to stack on top of #1226 with its 7 push-higher commits. Once #1226 is force-pushed, #1235 may need a rebase to stay clean but its content is the same.

## Mechanics

Done in this order to keep each step reversible:

1. **Capture safety reference**: tag the current state of both branches before mutating anything (`git tag claude-safety-2026-05-25-pre-split #1226-tip #1235-tip`). The existing `claude-safety-before-rollback` tag stays.
2. **Build the base branch from `main`**:
   - Branch `jimpelletier/eft-3295-async-migration-base` from `main`.
   - Apply file-level changes for the base PR scope, committing as a small number of logical commits (e.g., "Migrate SilentProcessRunner to async", "Migrate ISilentProcessRunner and CommandLineRunner", "Document sync↔async boundaries", "Use nameof for capability").
   - Push the branch and open the new PR with `main` as base.
3. **Rebuild #1226 on top of the base branch**:
   - Hard-reset `jimpelletier/eft-3295-tentacle-script-abandonment-to-release-the-mutex` to the new base branch's tip.
   - Apply file-level changes for the abandon feature scope (the delta from base PR's end state to `583eb46c`'s state).
   - Commit as a small number of logical commits.
   - Force-push #1226. GitHub will automatically update the PR diff to show only the abandon-feature delta.
4. **Rebase #1235**: change #1235's branch to be a rebase of its 7 commits on top of the updated #1226. Force-push.

## Risks

- **Force-push to #1226** will disrupt any in-flight reviews. PR comments referencing specific commit SHAs will become stale. Mitigated by: tagging the pre-split state for reference; explicitly noting in PR #1226 that history has been rewritten and pointing to the new base PR.
- **CI must pass on the base PR alone**. The base PR's `WaitForExitAsync(cancel)` wiring is a behavioural-equivalent of the sync version, so existing cancel tests (e.g. `ShouldCancelPing`) should pass. To be verified by running the build before opening the PR.
- **Compatibility with #1235**. The 7 push-higher commits build on file states that exist in #1226 today. After the rebase, those file states may shift slightly (e.g., the abandon PR no longer carries the same intermediate commit boundaries). Conflicts during the #1235 rebase are likely but should be mechanical to resolve.

## Verification

After the split:

- `git diff main..base-branch` produces a small focused diff matching the base PR scope above.
- `git diff base-branch..#1226` produces a diff matching the abandon-feature scope above.
- `git diff #1226..#1235` produces the existing 7-commit push-higher diff.
- Build the base branch standalone — must compile and CI must pass.
- Build #1226 stacked on base — must produce the same end-state as `583eb46c` does today.
- Build #1235 stacked on #1226 — must produce the same end-state as it does today.

## Success criteria

- New base PR exists at `jimpelletier/eft-3295-async-migration-base` with a clean, focused diff against `main`.
- PR #1226 is rebased to target the new base branch and its diff shows only the abandon feature.
- PR #1235 still works as a stacked PR on top of #1226.
- All three PRs build and pass CI.
