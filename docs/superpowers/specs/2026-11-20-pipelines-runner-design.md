# Pipelines runner in OctopusTentacle

## Intent

The runner was proven out as a PoC in `OctopusDeploy/infravnext`, branch `sahan/target-scale-poc`, folder `TargetScalePoC/` (see its `docs/runner-library-design.md`). It runs jobs pulled from a Server over a gRPC gateway and streams their logs straight to storage. It has three hosts:

- **Kubernetes runner** (AKS): pulls jobs, runs each as a pod, writes logs to Azure append blobs. Must not depend on any Tentacle code.
- **CLI**: built by another team in another repo, embeds the runner in-process and runs jobs in Docker, published with Native AOT. Must not depend on any Tentacle code.
- **Self-hosted runner**: runs next to a self-hosted Octopus Server.

Customers already install Tentacle on their machines. The self-hosted runner should reach them the same way: **they upgrade their installed Tentacle and get the runner for free**. So the self-hosted runner *is* Tentacle, and the runner's code has to live in this repo, in `Tentacle.sln`, so Tentacle can reference it. Moving it here is the first step; the rest is built out incrementally.

This is still a PoC. Fewer code paths beat configurability.

## Goals

1. The runner's libraries, the Kubernetes host and the CLI sample live in `source/` and build in `Tentacle.sln`, under the `Octopus.Pipelines.Runner` namespace.
2. The `net10.0` Tentacle runs the runner, **enabled by default**, reusing Tentacle's existing configuration: the polling server it already trusts. The gateway connection is unauthenticated for now (see Authentication).
3. `Octopus.Pipelines.Runner.Core` and `Octopus.Pipelines.Runner.Docker` can be packed as NuGet packages for the CLI team.
4. Runner tests run in this repo's build, and the CLI sample's Native AOT publish stays at zero warnings.

## Non-goals

- Publishing the NuGet packages anywhere (TeamCity/feed wiring). Pack only.
- Anything Server-side: `Poc.Server`, `Poc.LogStorage`, `Poc.Server.Tests`, the dashboard. They stay in infravnext.
- Infrastructure and deployment: `infra/`, `deploy/`, `scripts/` (cell provisioning), the storage capacity and isolation docs. They stay in infravnext.
- A separate self-hosted runner executable. `Poc.Runner.SelfHosted` is not brought over; Tentacle takes its place.
- The runner in the `net48` Tentacle. It simply doesn't have it.
- A runner-specific configuration command, a way to turn the runner off, or support for listening Tentacles.
- Sharing the gRPC contract with the Server via a package. The proto is copied; what happens next is decided later.
- Building and pushing the Kubernetes runner's container image from this repo's pipeline.

## Anti-goals

- Shared runner libraries (`Core`, `Docker`, `Gateway`, `Kubernetes`, `Logs.AzureBlob`, `Contracts`) referencing any `Octopus.Tentacle.*` project. Only the Tentacle glue references runner code, never the other way.
- Breaking or changing the `net48` Tentacle, its packages, or the existing `Release-<framework>-<runtime>` publish flow.
- `Core` or `Docker` gaining a trim/AOT warning (that would mean the CLI can't use them).
- Holding the new components back to match older choices elsewhere in the repo (e.g. downgrading packages or rewriting tests to NUnit).

## Constraints

- Same SDK on both sides (`10.0.403`). Repo-wide `Directory.Build.props` sets `TreatWarningsAsErrors` and `NuGetAuditMode=all`; runner projects inherit them.
- **No central package management here.** Every package version is pinned in the `.csproj` that uses it (the PoC's `Directory.Packages.props` doesn't come over).
- `Build.cs` publishes the **whole solution** once per `Release-<framework>-<runtime>` configuration (`dotnet publish source/Tentacle.sln -c Release-net48-win -f net48 ...`). Runner projects target `net10.0` only, so they must be excluded from `Release-net48-win` in the solution's configuration mapping.
- Tentacle (`source/Octopus.Tentacle`) uses Autofac 4.6, NLog, and its own command host; there is no .NET Generic Host. `RunAgentCommand` starts every Autofac-registered `IBackgroundTask` after Halibut and stops them on shutdown. That is where the runner plugs in.
- Tentacle authenticates to its Server with its own certificate (`ITentacleConfiguration.TentacleCertificate`) and trusts a Server by thumbprint (`TrustedOctopusServers[].Thumbprint`). A polling Tentacle also has the Server's `Address`; a listening Tentacle doesn't.
- Referencing `Logs.AzureBlob` brings `Azure.Storage.Blobs` (no `Azure.Identity`) into the net10 Tentacle's dependencies; that's accepted.
- `Octopus.Tentacle` already uses `KubernetesClient` 19.0.2 for the Kubernetes agent; the runner's Kubernetes library uses 21.0.8. The Tentacle glue doesn't reference the runner's Kubernetes library, so they don't meet.

## Approaches considered

| | Approach | Trade-off | Verdict |
|---|---|---|---|
| A | Runner in its own solution (`source/Octopus.Pipelines.Runner.slnx`) with its own build target | No risk to Tentacle's build, but Tentacle has to reference across solutions, and the first thing we want is exactly that reference | Rejected once "the self-hosted runner ships in Tentacle" was decided |
| **B** | Runner projects in `Tentacle.sln` in a `Pipelines.Runner` solution folder, excluded from the `net48` publish configuration; `Octopus.Tentacle` references them on `net10.0*` only | Every runner project must be mapped out of `Release-net48-win`; one solution to open | **Chosen.** It's what "upgrade Tentacle, get the runner" needs |

Turning the runner on: we considered (a) new settings in `tentacle.config` with a `configure-runner` command and (b) environment variables. **Neither**: we assume Tentacle's existing configuration is enough (it already knows its Server and how to authenticate), and the runner is on by default. Least code, fewest paths.

## Project layout

All under `source/`, in a `Pipelines.Runner` solution folder in `Tentacle.sln`. The PoC project → new project:

| PoC (`TargetScalePoC/src/…`) | Here (`source/…`) | Targets | Notes |
|---|---|---|---|
| `Poc.Runner.Core` | `Octopus.Pipelines.Runner.Core` | net10.0 | `IsAotCompatible`, packable |
| `Poc.Runner.Docker` | `Octopus.Pipelines.Runner.Docker` | net10.0 | `IsAotCompatible`, packable |
| `Poc.Runner.Gateway` | `Octopus.Pipelines.Runner.Gateway` | net10.0 | Gains a configurable reconnect backoff cap |
| `Poc.Runner.Kubernetes` | `Octopus.Pipelines.Runner.Kubernetes` | net10.0 | |
| `Poc.Runner.Logs.AzureBlob` | `Octopus.Pipelines.Runner.Logs.AzureBlob` | net10.0 | |
| `Poc.Contracts` (runner_gateway.proto only) | `Octopus.Pipelines.Runner.Contracts` | net10.0 | Proto copied verbatim; C# namespace option changed |
| `Poc.Runner` (AKS host exe) + its `Dockerfile` | `Octopus.Pipelines.Runner.Kubernetes.Host` | net10.0 | Same config keys as the PoC so infravnext's `deploy/runner/` keeps working |
| `Poc.Runner.CliSample` | `Octopus.Pipelines.Runner.CliSample` | net10.0 | `PublishAot=true`; the AOT gate |
| `Poc.Runner.Tests` | `Octopus.Pipelines.Runner.Tests` | net10.0 | Minus `SelfHostedCompositionTests` |
| — | `Octopus.Tentacle` (existing) | — | New `Pipelines/` folder, net10.0* only (below) |

Each project:

- Keeps the PoC's settings that the repo doesn't already set: `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`. Put these in a `source/Octopus.Pipelines.Runner.Directory.Build.props`-style shared props file *or* in each `.csproj`; builder's call, but it must not change any existing project. (A `Directory.Build.props` in `source/` would; don't.)
- Pins its package versions at the PoC's versions (from its `Directory.Packages.props`), except where Tentacle already pins a newer one.
- Namespaces become `Octopus.Pipelines.Runner.<Project>` (PoC: `TargetScalePoC.Runner.<Project>`). `InternalsVisibleTo` names follow.
- Is mapped into `Debug` and `Release` in `Tentacle.sln`. Only the libraries Tentacle references (`Core`, `Contracts`, `Gateway`, `Logs.AzureBlob`) are also built in the `Release-net10.0-<rid>` configurations; none is built in `Release-net48-win` or `Release-net10.0-windows-*`.

`Kubernetes.Host` and `CliSample` are executables. They must not produce output in `_build/` that Tentacle's packaging picks up. Builder to check: if publishing the solution for `Release-net10.0-<rid>` would publish them into Tentacle's artifacts, exclude them from those configurations too (they're built via their own targets, below).

## Tentacle integration

In `source/Octopus.Tentacle`, everything under `#if` / MSBuild conditions for non-`net48` targets:

- `ProjectReference` to `Octopus.Pipelines.Runner.Core`, `Octopus.Pipelines.Runner.Gateway` and `Octopus.Pipelines.Runner.Logs.AzureBlob`, conditioned on `'$(TargetFramework)' == 'net10.0'`. The `net10.0-windows` Tentacle doesn't reference it: it wouldn't start the runner (below), and the runner libraries only target `net10.0`.
- A `Pipelines/` folder with:
  - **`RunnerBackgroundTask : IBackgroundTask`**, registered in Autofac on net10.0* only (a small Autofac module). `Start()`:
    0. Returns without starting (logging once at Info why) when Tentacle is running as the Kubernetes agent (`KubernetesSupportDetection.IsRunningAsKubernetesAgent`; the Kubernetes runner is its own host, and jobs must not run as `sh -c` inside the agent pod), or on Windows (`ProcessJobBackend` runs `sh -c`, which a normal Windows machine doesn't have; a Windows shell is a later step). *Designer's call.*
    1. Picks the first entry in `TrustedOctopusServers` with `CommunicationStyle == TentacleActive` (polling) and an `Address`. If there's none (e.g. a listening Tentacle), logs once at Info that the runner isn't started and why, and returns.
    2. Builds the runner by hand, without a Generic Host: `ProcessJobBackend`, a `LogStores` with only the Azure Blob (`https`) store, `JobRunner`, the gateway's capacity provider, `GatewayJobExecutor` and `GatewayClient`.
    3. Starts `GatewayClient` (`StartAsync`). `Stop()` calls `StopAsync` with a bounded timeout. If `GatewayClient` as a `BackgroundService` can't be driven cleanly this way, give the Gateway library a small host-agnostic start/stop API instead; builder's call.
  - Logging bridged from `Microsoft.Extensions.Logging.ILogger` to Tentacle's `ISystemLog`, so runner logs land in the Tentacle log.
- **Settings, all derived from what Tentacle already has; no new configuration:**
  - Server address: the polling server's `Address`, used as-is as the gateway address. (Assumption: the Server serves the gateway there. Which port and path the real self-hosted Server uses is decided with the Server.)
  - Authentication: **none for now** (decided 2026-11-20, after the first build had mutual TLS with thumbprint pinning). The Tentacle sends no client certificate or token, and an `https://` address uses the platform's normal server-certificate validation. Both `http://` and `https://` polling addresses start the runner; any other scheme (e.g. `wss://`) is skipped with an Info line, because gRPC can't run over it. How the runner authenticates to a self-hosted Server is decided with the Server side.
  - Runner id: the Tentacle's `TentacleSquid`, falling back to the machine name.
  - Cell id: a constant `"self-hosted"`.
  - Capacity: `FixedCapacityProvider` with the PoC's default `MaxConcurrentJobs` (100). Adaptive starts off.
  - Job logs: the Server sends each job's storage location as an `https://` Azure Blob directory URI with its SAS (`JobStorage.directory_uri`), exactly as it does for the Kubernetes runner, and the Tentacle writes `job.log` there with the Azure Blob appender. No `file://` store is registered in Tentacle: a polling Tentacle isn't on the Server's machine, so a local file would be unreadable by the Server. A job given any other scheme fails with the existing unknown-scheme error. Other targets (a log collection endpoint, S3, …) are a later decision; for now it's Azure Blob only.
- **Gateway library change** to support the above: `GatewayOptions.MaxReconnectBackoff` (default 30s, which the Kubernetes host keeps); Tentacle sets 5 minutes so an unreachable Server isn't retried every 30s by every Tentacle. The PoC's optional bearer token stays for the Kubernetes host.
- Sandboxed jobs are refused, as `ProcessJobBackend` does in the PoC.

Every `net10.0*` Tentacle that polls a Server starts the runner and tries to connect. Against a Server without a gateway, it fails and retries with the PoC's backoff, logging each failure. That's accepted for the PoC; the builder should make sure retries are capped so they don't flood the log (e.g. log the first failure at Warn and later ones at Debug/Verbose until a connection succeeds).

## Contract

`runner_gateway.proto` is copied verbatim from the PoC branch head (`e7c2112`), changing only `option csharp_namespace` to `Octopus.Pipelines.Runner.Contracts`. A comment at the top records where it was copied from and that infravnext's copy is the Server's. No wire changes.

## Build

In `build/` (Nuke):

- `PackPipelinesRunner`: `dotnet pack` `Core` and `Docker` into `_artifacts/nuget`, versioned with the same `FullSemVer` as Tentacle's other packages (`PackCore` is the pattern). Not added to any publish step.
- `TestPipelinesRunner`: runs `Octopus.Pipelines.Runner.Tests` on Linux (Docker integration tests skip when no daemon is available, as in the PoC).
- `CheckPipelinesRunnerAot`: `dotnet publish source/Octopus.Pipelines.Runner.CliSample -c Release -r linux-x64 -p:PublishAot=true`, failing on any warning (port of the PoC's `scripts/check-aot.sh`).
- Wiring these targets into TeamCity is out of scope; say in the PR which targets exist.

The existing Tentacle unit tests gain tests for the Tentacle glue (picking the server, skipping when there's no polling server or its address can't carry gRPC). They run in the existing `Octopus.Tentacle.Tests` (NUnit), on its net10.0 runs only.

## Tests

- Runner tests keep **xunit v3**, as in the PoC. The repo's `global.json` doesn't opt into Microsoft.Testing.Platform, and `Build.Tests.cs` runs `dotnet test` with the VSTest `teamcity` logger, so the runner test project must also work under VSTest (`xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`). Don't change `global.json`.
- All PoC runner tests come over and pass, except:
  - `SelfHostedCompositionTests` (its host isn't coming);
  - `DeployedConfigurationTests` and `RunnerManifestTests`, which check infravnext's `deploy/`, `infra/` and `scripts/` files. Those files stay in infravnext, so these tests stay with them. *Designer's call.*
  `HostCompositionTests` points at the Kubernetes host.
- Tentacle glue tests as above.

## Order

1. Libraries, contract, tests: copied, renamed, building and green in `Tentacle.sln`, with `Release-net48-win` and the existing publish flow unchanged.
2. Kubernetes host and CLI sample, with the AOT gate and pack target.
3. Gateway reconnect backoff cap.
4. Tentacle integration (references, background task, logging bridge, tests).

Each step leaves the solution building and every existing test passing. Check after step 1 and step 4 that `Release-net48-win` and one `Release-net10.0-<rid>` publish still produce the same Tentacle output apart from the new runner assemblies in the net10 one.

## Left to the builder

- Where the shared runner MSBuild settings live (as long as no existing project changes).
- Whether `Kubernetes.Host`/`CliSample` need excluding from the per-RID publish configurations.
- How `GatewayClient` is started and stopped outside a Generic Host.
- The `ILogger` → Tentacle log bridge's shape.
- Package version conflicts in Tentacle's net10 dependency graph (e.g. `Microsoft.Extensions.*` 8.x vs 10.x): resolve by moving Tentacle's net10 references up, never by pinning runner packages down, and call each one out in the PR.
