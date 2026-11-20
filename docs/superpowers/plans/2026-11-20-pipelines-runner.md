# Pipelines Runner in Tentacle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the PoC runner into `Tentacle.sln` as `Octopus.Pipelines.Runner.*`, and have the net10.0 Tentacle run it by default against its polling server.

**Architecture:** Runner libraries are copied from the PoC with namespaces renamed, built in `Tentacle.sln` but kept out of the `net48` and `net10.0-windows` publish configurations. `Octopus.Tentacle` references four of them on `net10.0` only and starts the gateway loop from an Autofac-registered `IBackgroundTask`, deriving every setting from Tentacle's existing configuration and authenticating with the Tentacle's certificate.

**Tech Stack:** .NET 10 (SDK 10.0.403), gRPC (`Grpc.Net.Client`), `Azure.Storage.Blobs`, `KubernetesClient` 21.0.8, xunit v3 (runner tests), NUnit/NSubstitute/FluentAssertions (Tentacle tests), Autofac 4.6, Nuke.

**Spec:** `docs/superpowers/specs/2026-11-20-pipelines-runner-design.md`

## PoC source

The PoC is at `OctopusDeploy/infravnext`, branch `sahan/target-scale-poc`, commit `e7c2112`, folder `TargetScalePoC/`. Get it with:

```bash
git clone --filter=blob:none --no-checkout https://github.com/OctopusDeploy/infravnext.git "$TMP/infravnext"
git -C "$TMP/infravnext" fetch origin sahan/target-scale-poc
git -C "$TMP/infravnext" archive -o "$TMP/poc.tar" e7c2112 TargetScalePoC/src TargetScalePoC/tests TargetScalePoC/scripts/check-aot.sh TargetScalePoC/Directory.Packages.props
mkdir -p "$TMP/poc" && tar -xf "$TMP/poc.tar" -C "$TMP/poc"
```

`$POC` below means `$TMP/poc/TargetScalePoC`.

## Global Constraints

- No central package management: every `PackageReference` has a `Version` in its own `.csproj`. Versions from `$POC/Directory.Packages.props` (e.g. `Microsoft.Extensions.*` 10.0.14, `Grpc.*` 2.85.0, `Google.Protobuf` 3.36.2, `Azure.Storage.Blobs` 12.30.1, `KubernetesClient` 21.0.8, `xunit.v3` 4.0.2), unless Tentacle already pins a newer one.
- No existing project's build settings change. No `Directory.Build.props` under `source/`. Don't change `global.json`.
- Shared runner libraries never reference an `Octopus.Tentacle.*` project.
- `Octopus.Pipelines.Runner.Core` and `.Docker`: `IsAotCompatible=true`, zero trim/AOT warnings.
- `TreatWarningsAsErrors` and `NuGetAuditMode=all` (inherited from the root `Directory.Build.props`) stay on for runner projects. Fix warnings; don't suppress them.
- Package conflicts in Tentacle's net10.0 graph are resolved by moving Tentacle's references up, never by pinning runner packages down. List each one in the final PR description.
- No `Poc.Server`, `Poc.LogStorage`, infra, deploy or cell scripts come over.
- Don't amend commits. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Rename map

Apply to every copied file (C#, csproj, proto, Dockerfile), in this order:

| PoC | New |
|---|---|
| `TargetScalePoC.Runner.Tests` | `Octopus.Pipelines.Runner.Tests` |
| `TargetScalePoC.Runner.Core` / `.Docker` / `.Gateway` / `.Kubernetes` / `.Logs.AzureBlob` / `.CliSample` | `Octopus.Pipelines.Runner.Core` / … (same suffix) |
| `TargetScalePoC.Runner` (the AKS host's root namespace) | `Octopus.Pipelines.Runner.Kubernetes.Host` |
| `TargetScalePoC.Contracts` | `Octopus.Pipelines.Runner.Contracts` |
| `Poc.Runner.Tests` | `Octopus.Pipelines.Runner.Tests` |
| `Poc.Runner.<Suffix>` | `Octopus.Pipelines.Runner.<Suffix>` |
| `Poc.Runner` (AKS host project/assembly) | `Octopus.Pipelines.Runner.Kubernetes.Host` |
| `Poc.Contracts` | `Octopus.Pipelines.Runner.Contracts` |

The proto keeps `package targetscalepoc.v1;` (the wire name, shared with infravnext's Server). Only `option csharp_namespace` changes.

## Solution configuration rule

In `source/Tentacle.sln`, put every new project in a `Pipelines.Runner` solution folder. Each project's `ActiveCfg` for every solution configuration maps to `Debug|Any CPU` (for `Debug`) or `Release|Any CPU` (everything else), the same as `Octopus.Tentacle.Contracts` does today (lines 176–201). `Build.0` lines:

| Project | `Debug`, `Release` | `Release-net10.0-<rid>` (non-windows TFM) | `Release-net48-win`, `Release-net10.0-windows-*` |
|---|---|---|---|
| `.Core`, `.Contracts`, `.Gateway`, `.Logs.AzureBlob` (what Tentacle references) | build | build | **no `Build.0`** |
| `.Docker`, `.Kubernetes`, `.Kubernetes.Host`, `.CliSample`, `.Tests` | build | **no `Build.0`** | **no `Build.0`** |

Reason: `Build.cs` publishes the whole solution per configuration with `-f <framework> -r <rid>`. Runner projects only target `net10.0`, so they can't build for `net48` or `net10.0-windows`; and the exes (`CliSample` has `PublishAot=true`) must not be published into Tentacle's artifacts.

## Review Focus

1. **Tentacle shutting down while jobs run:** `Stop()` must return within its timeout (30s) even if a job is still running, so the Tentacle service stops cleanly. Test in Task 7.
2. **Start failing inside Tentacle:** any exception while building or starting the runner is logged and swallowed. Tentacle keeps running Halibut and its other background tasks. Test in Task 7.
3. **Server certificate doesn't match the trusted thumbprint:** the connection is refused, never trusted by default. Test in Task 5.
4. **No Tentacle certificate configured** (`TentacleCertificate == null`): the runner is skipped with a logged reason, with no `NullReferenceException`. Test in Task 6.
5. **Several trusted servers, the first listening and a later one polling:** the polling one is used. Test in Task 6.

---

### Task 1: Core and Docker libraries with their tests

**Files:**
- Create: `source/Octopus.Pipelines.Runner.props`
- Create: `source/Octopus.Pipelines.Runner.Core/**` (copied from `$POC/src/Poc.Runner.Core/**`)
- Create: `source/Octopus.Pipelines.Runner.Docker/**` (from `$POC/src/Poc.Runner.Docker/**`)
- Create: `source/Octopus.Pipelines.Runner.Tests/Octopus.Pipelines.Runner.Tests.csproj`
- Create, from `$POC/tests/Poc.Runner.Tests/`: `JobRunnerTests.cs`, `StreamingLogWriterTests.cs`, `FileLogStoreTests.cs`, `LogStoresTests.cs`, `ProcessJobBackendTests.cs`, `DockerJobBackendTests.cs`, `DockerIntegrationTests.cs`
- Modify: `source/Tentacle.sln`

**Interfaces:**
- Produces:
  - `source/Octopus.Pipelines.Runner.props`, imported by every runner `.csproj` with `<Import Project="..\Octopus.Pipelines.Runner.props" />`. It sets `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable` and `LangVersion=latest`.
  - Namespace `Octopus.Pipelines.Runner.Core`, unchanged from the PoC apart from the rename: `JobRunner(IJobBackend, ILogStore, JobRunnerOptions, TimeProvider?, ILogger?)`, `JobRunner.RunAsync(JobSpec, Uri jobDirectory, IJobObserver?, CancellationToken) : Task<JobResult>`, `ProcessJobBackend(TimeProvider)`, `Logs.LogStores(IReadOnlyDictionary<string, ILogStore>)`, `Logs.ILogStore`, `Logs.FileLogStore(string root)`.
  - `Octopus.Pipelines.Runner.Docker.DockerJobBackend`.

- [ ] **Step 1: Copy and rename.** Copy the two source projects and the seven test files. Apply the rename map. Each `.csproj` imports `Octopus.Pipelines.Runner.props`, drops `TargetFramework` and the settings the props file sets, and adds `Version` to every `PackageReference` (Core: `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Logging.Abstractions`, both 10.0.14). Set `IsPackable=true` on Core and Docker.
- [ ] **Step 2: Test project.** `OutputType=Exe`, `IsPackable=false`, and references to `xunit.v3` 4.0.2, `xunit.runner.visualstudio` (latest 3.x that supports xunit.v3 4.x), `Microsoft.NET.Test.Sdk` 18.11.0, plus `<Using Include="Xunit" />`. The VSTest packages are there because `Build.Tests.cs` drives `dotnet test` with the VSTest `teamcity` logger and `global.json` doesn't opt into Microsoft.Testing.Platform. Add project references to Core and Docker.
- [ ] **Step 3: Add the three projects to `Tentacle.sln`**, following the solution configuration rule above (use `dotnet sln add --solution-folder Pipelines.Runner`, then fix the configuration lines by hand).
- [ ] **Step 4: Run the tests**

Run: `dotnet test source/Octopus.Pipelines.Runner.Tests`
Expected: all pass. Docker integration tests are skipped if no Docker daemon is available, and pass if one is.

- [ ] **Step 5: Check the solution still builds for Tentacle's net10 publish**

Run: `dotnet build source/Tentacle.sln -c Release` and then `dotnet publish source/Tentacle.sln -c Release-net10.0-linux-x64 -f net10.0 -r linux-x64 --self-contained -maxcpucount:1`
Expected: both succeed. `_build/Tentacle/net10.0/linux-x64/` has no `Octopus.Pipelines.Runner.*.dll` yet, because nothing references the runner.

- [ ] **Step 6: Check the solution mapping**

Run: `grep -E "Release-net48-win\|Any CPU.Build.0|Release-net10.0-windows" source/Tentacle.sln` and check the output against the GUIDs of the new projects.
Expected: no `Build.0` lines for the new projects under `Release-net48-win` or `Release-net10.0-windows-*`.

- [ ] **Step 7: Commit.** Message: `Add the pipelines runner core and Docker libraries from the PoC`.

### Task 2: Contracts, Gateway, Azure Blob and Kubernetes libraries

**Files:**
- Create: `source/Octopus.Pipelines.Runner.Contracts/{Octopus.Pipelines.Runner.Contracts.csproj,Protos/runner_gateway.proto}`
- Create: `source/Octopus.Pipelines.Runner.Gateway/**`, `source/Octopus.Pipelines.Runner.Logs.AzureBlob/**`, `source/Octopus.Pipelines.Runner.Kubernetes/**` (from the PoC equivalents)
- Create in the test project, from the PoC: `GatewayJobExecutorTests.cs`, `StartingCapacityTests.cs`, `AdaptiveStartLimitTests.cs`, `AzureBlobLogStoreTests.cs`, `PodLogFollowerTests.cs`, `PodTrackerTests.cs`, `JobPodTests.cs`
- Modify: `source/Octopus.Pipelines.Runner.Tests/Octopus.Pipelines.Runner.Tests.csproj`, `source/Tentacle.sln`

**Interfaces:**
- Consumes: Task 1's Core namespace and props file.
- Produces:
  - `Octopus.Pipelines.Runner.Contracts`: generated `RunnerGateway.RunnerGatewayClient`, `ExecuteJobCommand` and the other messages.
  - `Octopus.Pipelines.Runner.Gateway`: `GatewayClient(GatewayJobExecutor, Outbox, ICapacityProvider, CapacitySignal, IOptions<GatewayOptions>, ILogger<GatewayClient>) : BackgroundService`, `GatewayJobExecutor(JobRunner, Outbox, CapacitySignal, TimeProvider, IHostApplicationLifetime, ILogger<GatewayJobExecutor>)` with `WhenAllCompleted() : Task`, `Outbox()`, `CapacitySignal()`, `FixedCapacityProvider(GatewayJobExecutor, IOptions<GatewayOptions>)`, `GatewayOptions` (`ServerAddress`, `Token`, `CellId`, `RunnerId`, `MaxConcurrentJobs`, …), `AddRunnerGateway(IServiceCollection, IConfiguration)`.
  - `Octopus.Pipelines.Runner.Logs.AzureBlob.AzureBlobLogStore(BlobClientOptions? options = null)`.

- [ ] **Step 1: Copy the proto verbatim** from `$POC/src/Poc.Contracts/Protos/runner_gateway.proto`. Change only `option csharp_namespace = "Octopus.Pipelines.Runner.Contracts";`, and add this header comment:

```proto
// Copied from OctopusDeploy/infravnext@e7c2112 TargetScalePoC/src/Poc.Contracts/Protos/runner_gateway.proto.
// The Server side's copy lives there. Keep the two in step until the contract has a single home.
```

Contracts `.csproj`: `Google.Protobuf` 3.36.2, `Grpc.Net.Client` 2.85.0, and `Grpc.Tools` 2.85.0 with `PrivateAssets="All"`. Use `<Protobuf Include="Protos\runner_gateway.proto" GrpcServices="Client" />`; the runner never hosts the service.
- [ ] **Step 2: Copy and rename** Gateway, Logs.AzureBlob and Kubernetes, pinning versions: `Microsoft.Extensions.Hosting` 10.0.14, `Azure.Storage.Blobs` 12.30.1, `KubernetesClient` 21.0.8. Add the test files and the project references the test project needs.
- [ ] **Step 3: Add the four projects to `Tentacle.sln`**, following the configuration rule.
- [ ] **Step 4: Run the tests**

Run: `dotnet test source/Octopus.Pipelines.Runner.Tests`
Expected: all pass.

- [ ] **Step 5: Repeat Task 1's Step 5 and Step 6 checks.** Expected: same results.
- [ ] **Step 6: Commit.** Message: `Add the pipelines runner gateway, contract, Azure Blob and Kubernetes libraries`.

### Task 3: Kubernetes runner host

**Files:**
- Create: `source/Octopus.Pipelines.Runner.Kubernetes.Host/{Octopus.Pipelines.Runner.Kubernetes.Host.csproj,Program.cs,RunnerHost.cs,RunnerOptions.cs,appsettings.json,Dockerfile}` (from `$POC/src/Poc.Runner/`)
- Create: `source/Octopus.Pipelines.Runner.Tests/HostCompositionTests.cs` (from the PoC, minus the test that reads `deploy/runner/appsettings.Production.json` at line 61, which needs `DeployedConfigurationTests.RepoRoot()`)
- Modify: `source/Tentacle.sln`, the test `.csproj`

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces: an executable `Octopus.Pipelines.Runner.Kubernetes.Host.dll` with the same `Runner:*` configuration keys as the PoC's `Poc.Runner`.

- [ ] **Step 1: Copy and rename.** Use `Sdk="Microsoft.NET.Sdk.Worker"` and `Microsoft.Extensions.Hosting` 10.0.14.
- [ ] **Step 2: Rewrite the Dockerfile** for a build context of the repo root: copy `global.json`, `Directory.Build.props`, `NuGet.Config`, `source/Octopus.Pipelines.Runner.props` and the six runner `.csproj` files it needs. Restore, copy the six project folders, then publish. Keep the PoC's `runtime:10.0-noble-chiseled` final image. Change `ENTRYPOINT` to `["dotnet", "Octopus.Pipelines.Runner.Kubernetes.Host.dll"]`. Add a header comment: `# Build context: repo root. docker build -f source/Octopus.Pipelines.Runner.Kubernetes.Host/Dockerfile .`
- [ ] **Step 3: Add the project to the solution** (Debug/Release only) **and run the tests**

Run: `dotnet test source/Octopus.Pipelines.Runner.Tests`
Expected: all pass, including `HostCompositionTests`.

- [ ] **Step 4: Build the image**

Run: `docker build -f source/Octopus.Pipelines.Runner.Kubernetes.Host/Dockerfile .`
Expected: success. Skip this step if Docker isn't available, and note that in the task report.

- [ ] **Step 5: Commit.** Message: `Add the pipelines runner Kubernetes host`.

### Task 4: CLI sample, AOT gate, test and pack targets

**Files:**
- Create: `source/Octopus.Pipelines.Runner.CliSample/{Octopus.Pipelines.Runner.CliSample.csproj,Program.cs,README.md}` (from the PoC)
- Create: `build/Build.PipelinesRunner.cs`
- Modify: `source/Tentacle.sln`

**Interfaces:**
- Consumes: Core and Docker.
- Produces: the Nuke targets `TestPipelinesRunner`, `CheckPipelinesRunnerAot` and `PackPipelinesRunner`.

- [ ] **Step 1: Copy and rename the CLI sample** (`PublishAot=true`, `InvariantGlobalization=true`). Add it to the solution, Debug/Release only. Update the README's paths and names.
- [ ] **Step 2: Add `build/Build.PipelinesRunner.cs`** (`partial class Build`) with three targets:
  - `TestPipelinesRunner`: `DotNetTest` on `SourceDirectory / "Octopus.Pipelines.Runner.Tests"` with loggers `console;verbosity=normal` and `teamcity`.
  - `CheckPipelinesRunnerAot`: a port of `$POC/scripts/check-aot.sh`. It works out the RID from the host architecture (`linux-x64` or `linux-arm64`, Linux only, with a clear failure message otherwise). It runs `dotnet clean`, then `dotnet publish … -c Release -r <rid> -p:PublishAot=true` into a temporary directory. It fails if the output matches `warning (IL|AOT)`. It then runs the binary with `--log-root <tmp> -- echo hi`, fails unless stdout is exactly `hi`, and logs the binary size and the median of five `-- true` runs.
  - `PackPipelinesRunner`: `DotNetPack` on Core and Docker into `ArtifactsDirectory / "nuget"`, with `.SetVersion(FullSemVer)` and `.DisableIncludeSymbols()`, following `PackCore`. It isn't added to `Pack` or any publish target.
- [ ] **Step 3: Run each target**

Run: `./build.sh --target TestPipelinesRunner`, `./build.sh --target CheckPipelinesRunnerAot` and `./build.sh --target PackPipelinesRunner`
Expected: tests pass. The AOT check prints `OK: publish produced zero IL/AOT warnings` and `OK: '-- echo hi' printed hi`. `_artifacts/nuget/` contains `Octopus.Pipelines.Runner.Core.<ver>.nupkg` and `Octopus.Pipelines.Runner.Docker.<ver>.nupkg`.

- [ ] **Step 4: Commit.** Message: `Add the pipelines runner CLI sample with AOT, test and pack build targets`.

### Task 5: Gateway client-certificate authentication and quieter reconnects

**Files:**
- Modify: `source/Octopus.Pipelines.Runner.Gateway/GatewayOptions.cs`, `GatewayClient.cs`
- Create: `source/Octopus.Pipelines.Runner.Gateway/GatewayHttpHandler.cs`
- Test: `source/Octopus.Pipelines.Runner.Tests/GatewayHttpHandlerTests.cs`

**Interfaces:**
- Produces:
  - Two new properties on `GatewayOptions`, both default `null`: `X509Certificate2? ClientCertificate` and `Func<X509Certificate2, bool>? ServerCertificateValidator`. Mark both `[JsonIgnore]`-style non-bindable if the configuration binder complains; `HostCompositionTests` must still pass.
  - `internal static SocketsHttpHandler GatewayHttpHandler.Create(GatewayOptions options)`, which builds the PoC's handler (same keep-alive settings) plus:
    - when `ClientCertificate` is set, `SslOptions.ClientCertificates = [cert]`;
    - when `ServerCertificateValidator` is set, `SslOptions.RemoteCertificateValidationCallback = (_, cert, _, _) => cert is not null && validator(new X509Certificate2(cert))`. Chain errors are ignored because the thumbprint is what's trusted, as with Halibut.
  - `GatewayClient` uses `GatewayHttpHandler.Create`.

- [ ] **Step 1: Write the failing tests** (self-signed certificates via `CertificateRequest`):

```csharp
[Fact] public void Client_certificate_is_offered_when_configured()
    // Create(new GatewayOptions { ClientCertificate = cert }).SslOptions.ClientCertificates contains cert
[Fact] public void No_client_certificate_by_default()
    // Create(new GatewayOptions()).SslOptions.ClientCertificates is null or empty
[Fact] public void Server_certificate_matching_the_validator_is_accepted()
    // callback(null!, serverCert, null, SslPolicyErrors.RemoteCertificateChainErrors) == true when validator returns cert.Thumbprint == serverCert.Thumbprint
[Fact] public void Server_certificate_not_matching_the_validator_is_refused()   // Review Focus 3
    // same validator, a different certificate → false
[Fact] public void Default_validation_is_left_to_the_platform()
    // Create(new GatewayOptions()).SslOptions.RemoteCertificateValidationCallback is null
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test source/Octopus.Pipelines.Runner.Tests --filter FullyQualifiedName~GatewayHttpHandlerTests`
Expected: FAIL (`GatewayHttpHandler` doesn't exist).

- [ ] **Step 3: Implement `GatewayHttpHandler.Create`** and use it from `GatewayClient.ExecuteAsync`.
- [ ] **Step 4: Make reconnect failures quieter.** In `GatewayClient`, the first consecutive failed connection or session logs at Warning, as today. Later consecutive failures log at Debug. The count resets when `Welcome` arrives (the existing `onWelcome` callback). Add `GatewayClientTests.Repeated_connection_failures_warn_once`: point `ServerAddress` at a closed local port, capture logs with a test `ILogger`, wait for three attempts (shrink the backoff through an `internal` test hook if needed), and assert exactly one Warning.
- [ ] **Step 5: Run the whole runner test suite**

Run: `dotnet test source/Octopus.Pipelines.Runner.Tests`
Expected: all pass.

- [ ] **Step 6: Commit.** Message: `Let the pipelines runner gateway authenticate with a client certificate and pin the server's`.

### Task 6: Derive runner settings from Tentacle's configuration

**Files:**
- Create: `source/Octopus.Tentacle/Pipelines/TentacleRunnerSettings.cs` (inside `#if PIPELINES_RUNNER`)
- Test: `source/Octopus.Tentacle.Tests/Pipelines/TentacleRunnerSettingsFixture.cs` (inside `#if PIPELINES_RUNNER`)
- Modify: `source/Octopus.Tentacle/Octopus.Tentacle.csproj`

**Interfaces:**
- Consumes: `ITentacleConfiguration` (`TrustedOctopusServers`, `TentacleCertificate`, `TentacleSquid`), `OctopusServerConfiguration` (`CommunicationStyle`, `Address`, `Thumbprint`), and `CommunicationStyle.TentacleActive` from `Octopus.Client.Model`.
- Produces:

```csharp
namespace Octopus.Tentacle.Pipelines;

public sealed record TentacleRunnerSettings(Uri ServerAddress, string ServerThumbprint, X509Certificate2 ClientCertificate, string RunnerId)
{
    public const string CellId = "self-hosted";
    public const int MaxConcurrentJobs = 100;

    // Returns null and sets skipReason (one human-readable sentence) when the runner shouldn't start.
    public static TentacleRunnerSettings? From(ITentacleConfiguration configuration, bool isKubernetesAgent, bool isWindows, out string? skipReason);
}
```

- [ ] **Step 1: Add the runner references to `Octopus.Tentacle.csproj`:** `ProjectReference`s to `Octopus.Pipelines.Runner.Core`, `.Gateway` and `.Logs.AzureBlob`, in an `ItemGroup` with `Condition="'$(TargetFramework)' == 'net10.0'"`. Under the same condition, add `PIPELINES_RUNNER` to `DefineConstants`. All new Tentacle code and tests are wrapped in `#if PIPELINES_RUNNER` (and `Octopus.Tentacle.Tests` gets the same constant for its `net10.0` target). Neither `net48` nor `net10.0-windows` references the runner. The Windows Tentacle wouldn't start it anyway, and the runner libraries don't target `net10.0-windows`.
- [ ] **Step 2: Write the failing tests** (NUnit + FluentAssertions; `ITentacleConfiguration` via NSubstitute):

```csharp
[Test] public void UsesTheFirstPollingServer()
    // servers: [listening(thumb A), polling(thumb B, https://b:10943), polling(thumb C)] → ServerAddress https://b:10943, ServerThumbprint "B"   // Review Focus 5
[Test] public void SkipsWhenThereIsNoPollingServer()
    // only listening servers → null, skipReason contains "polling"
[Test] public void SkipsWhenThePollingServerHasNoAddress()
    // polling, Address null → null
[Test] public void SkipsWhenTentacleHasNoCertificate()
    // TentacleCertificate null → null, skipReason contains "certificate"   // Review Focus 4
[Test] public void SkipsOnTheKubernetesAgent()
    // isKubernetesAgent: true → null, skipReason contains "Kubernetes agent"
[Test] public void SkipsOnWindows()
    // isWindows: true → null, skipReason contains "Windows"
[Test] public void RunnerIdIsTheTentacleSquid()
    // TentacleSquid "SQ-ABC" → RunnerId "SQ-ABC"; null → Environment.MachineName
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test source/Octopus.Tentacle.Tests -f net10.0 --filter FullyQualifiedName~TentacleRunnerSettingsFixture`
Expected: FAIL (compile error, `TentacleRunnerSettings` missing).

- [ ] **Step 4: Implement `TentacleRunnerSettings.From`.** The checks run in this order: Kubernetes agent, Windows, certificate, polling server with an address.
- [ ] **Step 5: Run the tests**, with the Step 3 command. Expected: PASS.
- [ ] **Step 6: Commit.** Message: `Derive pipelines runner settings from Tentacle's configuration`.

### Task 7: Run the runner as a Tentacle background task

**Files:**
- Create in `source/Octopus.Tentacle/Pipelines/`, all inside the Task 6 symbol: `RunnerBackgroundTask.cs`, `RunnerLifetime.cs`, `SystemLogLoggerProvider.cs`, `PipelinesRunnerModule.cs`
- Modify: `source/Octopus.Tentacle/Program.cs` (register the module under the same symbol)
- Test: `source/Octopus.Tentacle.Tests/Pipelines/RunnerBackgroundTaskFixture.cs`, `SystemLogLoggerProviderFixture.cs`

**Interfaces:**
- Consumes: Task 6's `TentacleRunnerSettings.From`; Task 5's `GatewayOptions.ClientCertificate` and `ServerCertificateValidator`; Gateway, Core and AzureBlob types from Tasks 1–2; `IBackgroundTask` (`Start()`/`Stop()`); `ISystemLog`; `KubernetesSupportDetection.IsRunningAsKubernetesAgent`.
- Produces:
  - `RunnerLifetime : IHostApplicationLifetime`. `StopApplication()` cancels `ApplicationStopping`. `ApplicationStarted` and `ApplicationStopped` are tokens that are never cancelled.
  - `SystemLogLoggerProvider(ISystemLog log) : ILoggerProvider`. It maps Trace→`Trace`, Debug→`Verbose`, Information→`Info`, Warning→`Warn`, Error and Critical→`Error`, and prefixes each message with `[Pipelines runner] `.
  - `RunnerBackgroundTask(ITentacleConfiguration, ISystemLog)`, with an internal constructor that also takes `Func<bool> isKubernetesAgent`, `Func<bool> isWindows`, a `TimeSpan stopTimeout` (default 30s) and an optional `IJobBackend` for tests.
  - `PipelinesRunnerModule : Autofac.Module`, which registers `RunnerBackgroundTask` `.As<IBackgroundTask>().SingleInstance()`.

`Start()` behaviour:
1. Calls `TentacleRunnerSettings.From`. On `null`, it logs `skipReason` once at Info and returns.
2. Builds the runner with `new`:
   - `ProcessJobBackend(TimeProvider.System)`;
   - `new LogStores(new Dictionary<string, ILogStore> { ["https"] = new AzureBlobLogStore() })`;
   - `JobRunner` with default `JobRunnerOptions`;
   - `Outbox`, `CapacitySignal`, `RunnerLifetime` and `GatewayJobExecutor`;
   - `FixedCapacityProvider` and `GatewayClient`, using `Options.Create(new GatewayOptions { ServerAddress = settings.ServerAddress.ToString(), CellId = TentacleRunnerSettings.CellId, RunnerId = settings.RunnerId, MaxConcurrentJobs = TentacleRunnerSettings.MaxConcurrentJobs, ClientCertificate = settings.ClientCertificate, ServerCertificateValidator = c => string.Equals(c.Thumbprint, settings.ServerThumbprint, StringComparison.OrdinalIgnoreCase) })`;
   - loggers from a `LoggerFactory` built with `SystemLogLoggerProvider`.
3. Calls `GatewayClient.StartAsync(CancellationToken.None)` and logs at Info: `Pipelines runner {RunnerId} started, polling {ServerAddress}`.
4. The whole method is wrapped: an exception is logged at Error and swallowed, and the task ends up not running.

`Stop()` behaviour: does nothing if the runner never started. Otherwise it calls `lifetime.StopApplication()`, which cancels running jobs through `ApplicationStopping`. It then calls `GatewayClient.StopAsync` with a token that cancels after `stopTimeout`, and waits on `executor.WhenAllCompleted()` for whatever is left of `stopTimeout`. It disposes the logger factory, and it never throws.

- [ ] **Step 1: Write the failing tests:**

```csharp
// RunnerBackgroundTaskFixture
[Test] public void StartDoesNothingWhenSettingsSaySkip()
    // listening-only config → Start() returns, ISystemLog.Received().Info(contains "polling"), Stop() doesn't throw
[Test] public void StartSwallowsFailures()   // Review Focus 2
    // config whose TrustedOctopusServers getter throws → Start() doesn't throw, ISystemLog.Received().Error(Arg.Any<Exception>(), Arg.Any<string>())
[Test] public void StartsAndStopsAgainstAnUnreachableServer()
    // polling server https://127.0.0.1:<closed port>, self-signed cert → Start() logs "Pipelines runner", Stop() returns within 5s
[Test] public void StopReturnsWithinTheTimeoutWhileAJobIsStillRunning()   // Review Focus 1
    // inject an IJobBackend whose RunAsync ignores cancellation and never completes; drive one job through GatewayJobExecutor.Start
    // (via an internal hook exposing the executor); stopTimeout 2s → Stop() returns in < 4s
[Test] public void ModuleRegistersTheTaskAsABackgroundTask()
    // ContainerBuilder + PipelinesRunnerModule + substitutes → Resolve<IEnumerable<IBackgroundTask>>() contains RunnerBackgroundTask

// SystemLogLoggerProviderFixture
[TestCase(LogLevel.Information, "Info")] [TestCase(LogLevel.Warning, "Warn")] [TestCase(LogLevel.Debug, "Verbose")] [TestCase(LogLevel.Error, "Error")]
public void MapsLevels(LogLevel level, string method)
    // logger.Log(level, "hi") → ISystemLog.<method>("[Pipelines runner] hi")
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test source/Octopus.Tentacle.Tests -f net10.0 --filter "FullyQualifiedName~Pipelines"`
Expected: FAIL (the types don't exist).

- [ ] **Step 3: Implement** `RunnerLifetime`, `SystemLogLoggerProvider`, `RunnerBackgroundTask` and `PipelinesRunnerModule`, then register the module in `Program.cs` after `MaintenanceModule`. If Tentacle's direct references conflict with the runner's 10.x `Microsoft.Extensions.*` (restore errors `NU1605`/`NU1608`), bump Tentacle's references for `net10.0` and note each bump for the PR description.
- [ ] **Step 4: Run the new tests**, with the Step 2 command. Expected: PASS.
- [ ] **Step 5: Run Tentacle's unit tests on net10.0**

Run: `dotnet test source/Octopus.Tentacle.Tests -f net10.0`
Expected: same pass/fail set as on `origin/main`. Record any failures that predate this change.

- [ ] **Step 6: Check Tentacle's publish output**

Run: `dotnet publish source/Tentacle.sln -c Release-net10.0-linux-x64 -f net10.0 -r linux-x64 --self-contained -maxcpucount:1`
Expected:
- `_build/Tentacle/net10.0/linux-x64/` contains `Octopus.Pipelines.Runner.Core.dll`, `.Gateway.dll`, `.Contracts.dll`, `.Logs.AzureBlob.dll` and `Azure.Storage.Blobs.dll`;
- it does **not** contain `Octopus.Pipelines.Runner.Kubernetes*.dll`, `.Docker.dll` or `.CliSample*`;
- the `net48` and `net10.0-windows` builds are unaffected: on a machine that can build them (Windows, or wherever CI builds them), `dotnet build source/Octopus.Tentacle -f net48` and `-f net10.0-windows` succeed and their output has no `Octopus.Pipelines.Runner.*.dll`. If no such machine is available, say so in the task report and rely on the condition plus the solution mapping check from Task 1.

- [ ] **Step 7: Smoke test by hand.** Configure a local polling Tentacle against any Server (or a fake address), run it, and confirm the Tentacle log shows `Pipelines runner … started, polling …` and one Warning for the failed gateway connection. Then stop it and confirm it shuts down within 30s.
- [ ] **Step 8: Commit.** Message: `Run the pipelines runner inside the net10.0 Tentacle by default`.
