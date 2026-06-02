---
name: run-windows-tests
description: Use when running a Windows-only test from this macOS/Apple-Silicon checkout — e.g. [WindowsTest]-attributed tests like the grandchild process-tree tests in SilentProcessRunnerFixture, or any test that skips locally because RuntimeInformation.IsOSPlatform(Windows) is false. Runs them on a GitHub Actions windows-latest runner.
---

# Run Windows-only tests on a GitHub Actions Windows runner

## Overview

Windows-only tests (`[WindowsTest]`) compile on macOS but **skip at runtime** — they need
a real Windows host (they spawn `cmd.exe`, `ping.exe`, query WMI). This skill runs them on
a **GitHub Actions `windows-latest`** runner and streams the result back. `github.com` is
reachable from the sandbox, so I can trigger and watch the run to completion myself — no
local VM, no handing it back.

## When to use

- A test is `[WindowsTest]` / shows as skipped locally and you need its real result.
- You're iterating on Windows process/abandonment behavior in `SilentProcessRunner`.
- NOT for tests that run on macOS — run those with `dotnet test` directly.

## How to run

```bash
.claude/skills/run-windows-tests/run.sh "<filter>"
```

The filter is **required** (no default) and is a standard `dotnet test --filter` expression.
You (the agent) construct it for the test you actually want — don't copy the example below
verbatim. Common shapes:

- `Name~<substring>` — method name contains the substring. Prefer a substring stable across
  branches if the method might be renamed, e.g. `Name~WhenGrandchildHoldsRedirectedPipes`
  matches both `CancellationToken_WhenGrandchild…` (main) and `CancelThenAbandon_WhenGrandchild…` (a feature branch).
- `FullyQualifiedName~<Namespace.Class>` — run a whole fixture, e.g.
  `FullyQualifiedName~SilentProcessRunnerFixture`.
- Combine with `&` / `|` as `dotnet test --filter` supports.

`run.sh` dispatches `.github/workflows/windows-test.yml` against the current branch, then
`gh run watch`es it and exits with the run's status.

**Two operational facts:**
- **Run it with the sandbox disabled.** `gh`'s HTTPS to api.github.com fails TLS under the
  command sandbox (proxy), so `gh` calls need the sandbox off.
- **`workflow_dispatch` needs the workflow on the default branch.** It's manual-dispatch
  only (no `push` trigger), so it can't run until `windows-test.yml` is merged to `main`.
  Once merged, `--ref <branch>` runs that branch's code (e.g. EFT-3295's renamed test).

## The workflow

`.github/workflows/windows-test.yml`: `workflow_dispatch` only, with a **required** `filter`
input (passed via `env:` to avoid script injection). `windows-latest`, `actions/setup-dotnet`
honoring `global.json` (SDK 8.0.413), then `dotnet test` on
`Octopus.Tentacle.Tests.Integration` (`--framework net8.0 --filter <filter>`).

## Common mistakes

- Running `dotnet test` on the Mac and reporting "passed" — it **skipped**. Use this skill.
- Calling `gh` inside the sandbox — TLS fails; disable the sandbox for `gh`.
- Expecting `workflow_dispatch` to work before the workflow is on the default branch.
