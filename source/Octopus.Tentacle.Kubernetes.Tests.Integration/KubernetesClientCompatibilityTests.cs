using System;
using FluentAssertions;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators.Proxies;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[TestFixture]
public class KubernetesClientCompatibilityTests
{
    static readonly object[] TestClusterVersions =
    [
        new object[] {new ClusterVersion(1, 35)},
        new object[] {new ClusterVersion(1, 34)},
        new object[] {new ClusterVersion(1, 33)}
    ];

    // The tools are downloaded once for the whole fixture; everything else is per test case and so is
    // built up by SetUp and owned (and disposed) by the TestRun it hands back.
    readonly TemporaryDirectory toolsTemporaryDirectory = new();

    readonly ILogger logger = new SerilogLoggerBuilder().Build();
    RequiredTools? requiredTools;

    RequiredTools RequiredTools => requiredTools ?? throw new InvalidOperationException("Expected the required tools to have been downloaded by OneTimeSetup");

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        var toolDownloader = new RequiredToolDownloader(toolsTemporaryDirectory, logger);
        requiredTools = await toolDownloader.DownloadRequiredTools(CancellationToken.None);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        toolsTemporaryDirectory.Dispose();
    }

    [Test]
    [TestCaseSource(nameof(TestClusterVersions))]
    public async Task RunSimpleScript(ClusterVersion clusterVersion)
    {
        await using var testRun = await SetUp(clusterVersion);
        var tentacleClient = testRun.TentacleClient;
        var recordedMethodUsages = testRun.RecordedMethodUsages;
        var cancellationToken = testRun.CancellationToken;

        // Arrange
        var logs = new List<ProcessOutput>();
        var scriptCompleted = false;

        var builder = new ExecuteKubernetesScriptCommandBuilder(LoggingUtils.CurrentTestHash())
            .WithScriptBody(script => script
                .Print("Hello World")
                .PrintNTimesWithDelay("Yep", 30, TimeSpan.FromMilliseconds(100)));

        var command = builder.Build();

        // Act
        var result = await tentacleClient.ExecuteScript(command, StatusReceived, ScriptCompleted, new InMemoryLog(), cancellationToken);

        // Assert
        logs.Should().Contain(po => po.Text.StartsWith("[POD EVENT]")); // Verify that we are receiving some pod events
        logs.Should().Contain(po => po.Source == ProcessOutputSource.StdOut && po.Text == "Hello World");
        scriptCompleted.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.State.Should().Be(ProcessState.Complete);

        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.StartScriptAsync)).Started.Should().Be(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.GetStatusAsync)).Started.Should().BeGreaterThan(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.CompleteScriptAsync)).Started.Should().Be(1);
        recordedMethodUsages.For(nameof(IAsyncClientKubernetesScriptServiceV1.CancelScriptAsync)).Started.Should().Be(0);

        return;

        void StatusReceived(ScriptExecutionStatus status)
        {
            logs.AddRange(status.Logs);
        }

        Task ScriptCompleted(CancellationToken ct)
        {
            scriptCompleted = true;
            return Task.CompletedTask;
        }
    }
    
    async Task<TestRun> SetUp(ClusterVersion clusterVersion)
    {
        // Each resource goes onto the TestRun as soon as it's created, so if SetUp fails part way through,
        // disposing the TestRun cleans up whatever did get created.
        var testRun = new TestRun(logger);
        try
        {
            // Built first, and handed to this test case's context, so the cluster and agent setup and
            // teardown for this test case land in its own trace log.
            var traceLogFileLogger = new TraceLogFileLogger(LoggingUtils.CurrentTestHash());
            testRun.TraceLogFileLogger = traceLogFileLogger;
            var testLogger = new SerilogLoggerBuilder()
                .SetTraceLogFileLogger(traceLogFileLogger)
                .Build()
                .ForContext(GetType());

            var context = new KubernetesTestsGlobalContext(testLogger);
            testRun.Context = context;

            await SetupCluster(testRun, context, clusterVersion);

            var agentInstaller = new KubernetesAgentInstaller(
                context.TemporaryDirectory,
                context.HelmExePath,
                context.KubeCtlExePath,
                context.KubeConfigPath,
                context.Logger);
            testRun.AgentInstaller = agentInstaller;

            //create a new server halibut runtime
            var halibutRuntime = SetupHelpers.BuildServerHalibutRuntime();
            testRun.ServerHalibutRuntime = halibutRuntime;

            var listeningPort = halibutRuntime.Listen();

            var agentThumbprint = await agentInstaller.InstallAgent(listeningPort, context.TentacleImageAndTag, new Dictionary<string, string>());

            //trust the generated cert thumbprint
            halibutRuntime.Trust(agentThumbprint);

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));
            testRun.CancellationTokenSource = cancellationTokenSource;

            IRecordedMethodUsages? recordedMethodUsages = null;
            testRun.TentacleClient = SetupHelpers.BuildTentacleClient(agentInstaller.SubscriptionId, agentThumbprint, halibutRuntime, builder =>
            {
                builder.RecordMethodUsages<IAsyncClientKubernetesScriptServiceV1>(out var recordedUsages);
                recordedMethodUsages = recordedUsages;
            });
            testRun.RecordedMethodUsages = recordedMethodUsages ?? throw new InvalidOperationException("Expected the tentacle service decorator builder to have recorded the method usages");

            return testRun;
        }
        catch
        {
            await testRun.DisposeAsync();
            throw;
        }
    }

    async Task SetupCluster(TestRun testRun, KubernetesTestsGlobalContext context, ClusterVersion clusterVersion)
    {
        var tools = RequiredTools;

        var clusterInstaller = new KubernetesClusterInstaller(context.TemporaryDirectory, tools.KindExePath, tools.HelmExePath, tools.KubeCtlPath, context.Logger);
        testRun.ClusterInstaller = clusterInstaller;
        await clusterInstaller.Install(clusterVersion);

        context.TentacleImageAndTag = await SetupHelpers.GetTentacleImageAndTag(tools.KindExePath, clusterInstaller);
        context.SetToolExePaths(tools.HelmExePath, tools.KubeCtlPath);
        context.KubeConfigPath = clusterInstaller.KubeConfigPath;
    }

    /// <summary>
    /// The per-test-case state that <see cref="SetUp"/> builds. The test holds it with <c>await using</c>,
    /// so everything it owns is cleaned up when the test case finishes.
    /// </summary>
    sealed class TestRun(ILogger fallbackLogger) : IAsyncDisposable
    {
        TentacleClient? tentacleClient;
        IRecordedMethodUsages? recordedMethodUsages;

        public KubernetesTestsGlobalContext? Context { get; set; }
        public KubernetesClusterInstaller? ClusterInstaller { get; set; }
        public KubernetesAgentInstaller? AgentInstaller { get; set; }
        public HalibutRuntime? ServerHalibutRuntime { get; set; }
        public TraceLogFileLogger? TraceLogFileLogger { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public TentacleClient TentacleClient
        {
            get => tentacleClient ?? throw new InvalidOperationException("Expected SetUp to have built the tentacle client");
            set => tentacleClient = value;
        }

        public IRecordedMethodUsages RecordedMethodUsages
        {
            get => recordedMethodUsages ?? throw new InvalidOperationException("Expected SetUp to have recorded the method usages");
            set => recordedMethodUsages = value;
        }

        // The test case's own logger once SetUp has made it, so cleanup failures land in its trace log.
        ILogger Logger => Context?.Logger ?? fallbackLogger;

        public CancellationToken CancellationToken => CancellationTokenSource?.Token ?? throw new InvalidOperationException("Expected SetUp to have created the cancellation token source");

        // Never throws: the test disposes this with `await using`, so an exception from here would replace the
        // test's own failure. Instead every step runs, even if an earlier one failed, and failures are logged.
        public async ValueTask DisposeAsync()
        {
            await RunCleanupStep("cancel the test's cancellation token", async () =>
            {
                if (CancellationTokenSource is null) return;
                await CancellationTokenSource.CancelAsync();
                CancellationTokenSource.Dispose();
            });

            // Order matters: the agent is uninstalled with helm against the cluster, so it has to go before
            // the cluster installer deletes the cluster out from under it.
            await RunCleanupStep("dispose the server Halibut runtime", async () =>
            {
                if (ServerHalibutRuntime is not null) await ServerHalibutRuntime.DisposeAsync();
            });
            await RunCleanupStep("uninstall the agent", () => AgentInstaller?.Dispose());
            await RunCleanupStep("delete the kind cluster", () => ClusterInstaller?.Dispose());
            await RunCleanupStep("dispose the test context", () => Context?.Dispose());

            // Last, so everything above still gets written to the test case's trace log.
            try
            {
                if (TraceLogFileLogger is not null) await TraceLogFileLogger.DisposeAsync();
            }
            catch (Exception e)
            {
                fallbackLogger.Error(e, "Failed to dispose the trace log file logger while cleaning up the test run");
            }
        }

        Task RunCleanupStep(string description, Action step) => RunCleanupStep(description, () =>
        {
            step();
            return Task.CompletedTask;
        });

        async Task RunCleanupStep(string description, Func<Task> step)
        {
            try
            {
                await step();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to {CleanupStep} while cleaning up the test run", description);
            }
        }
    }
}
