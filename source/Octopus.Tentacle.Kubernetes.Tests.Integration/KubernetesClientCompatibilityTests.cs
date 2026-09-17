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
    // built up (and handed back as locals) by SetUp.
    readonly TemporaryDirectory toolsTemporaryDirectory = new();

    ILogger logger = new SerilogLoggerBuilder().Build();
    RequiredTools? requiredTools;

    // Fields only because TearDown has to clean them up after the test case has finished with them.
    KubernetesTestsGlobalContext? testContext;
    KubernetesClusterInstaller? clusterInstaller;
    TraceLogFileLogger? traceLogFileLogger;
    CancellationTokenSource? cancellationTokenSource;

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

    [TearDown]
    public async Task TearDown()
    {
        if (traceLogFileLogger is not null) await traceLogFileLogger.DisposeAsync();
        if (cancellationTokenSource is not null)
        {
            await cancellationTokenSource.CancelAsync();
            cancellationTokenSource.Dispose();
        }
        clusterInstaller?.Dispose();
        testContext?.Dispose();

        traceLogFileLogger = null;
        cancellationTokenSource = null;
        clusterInstaller = null;
        testContext = null;
    }

    [Test]
    [TestCaseSource(nameof(TestClusterVersions))]
    public async Task RunSimpleScript(ClusterVersion clusterVersion)
    {
        var (tentacleClient, recordedMethodUsages, cancellationToken) = await SetUp(clusterVersion);

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
        var context = new KubernetesTestsGlobalContext(logger);
        testContext = context;

        await SetupCluster(context, clusterVersion);

        var kubernetesAgentInstaller = new KubernetesAgentInstaller(
            context.TemporaryDirectory,
            context.HelmExePath,
            context.KubeCtlExePath,
            context.KubeConfigPath,
            context.Logger);

        //create a new server halibut runtime
        var serverHalibutRuntime = SetupHelpers.BuildServerHalibutRuntime();
        var listeningPort = serverHalibutRuntime.Listen();

        var agentThumbprint = await kubernetesAgentInstaller.InstallAgent(listeningPort, context.TentacleImageAndTag, new Dictionary<string, string>());

        //trust the generated cert thumbprint
        serverHalibutRuntime.Trust(agentThumbprint);

        traceLogFileLogger = new TraceLogFileLogger(LoggingUtils.CurrentTestHash());
        logger = new SerilogLoggerBuilder()
            .SetTraceLogFileLogger(traceLogFileLogger)
            .Build()
            .ForContext(GetType());

        var testCancellationTokenSource = new CancellationTokenSource();
        testCancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));
        cancellationTokenSource = testCancellationTokenSource;

        IRecordedMethodUsages? recordedMethodUsages = null;
        var tentacleClient = SetupHelpers.BuildTentacleClient(kubernetesAgentInstaller.SubscriptionId, agentThumbprint, serverHalibutRuntime, builder =>
        {
            builder.RecordMethodUsages<IAsyncClientKubernetesScriptServiceV1>(out var recordedUsages);
            recordedMethodUsages = recordedUsages;
        });

        return new TestRun(
            tentacleClient,
            recordedMethodUsages ?? throw new InvalidOperationException("Expected the tentacle service decorator builder to have recorded the method usages"),
            testCancellationTokenSource.Token);
    }
    
    async Task SetupCluster(KubernetesTestsGlobalContext context, ClusterVersion clusterVersion)
    {
        var tools = RequiredTools;

        clusterInstaller = new KubernetesClusterInstaller(context.TemporaryDirectory, tools.KindExePath, tools.HelmExePath, tools.KubeCtlPath, context.Logger);
        await clusterInstaller.Install(clusterVersion);

        context.TentacleImageAndTag = await SetupHelpers.GetTentacleImageAndTag(tools.KindExePath, clusterInstaller);
        context.SetToolExePaths(tools.HelmExePath, tools.KubeCtlPath);
        context.KubeConfigPath = clusterInstaller.KubeConfigPath;
    }

    /// <summary>The per-test-case state that <see cref="SetUp"/> builds, so the test can hold it in locals.</summary>
    sealed record TestRun(TentacleClient TentacleClient, IRecordedMethodUsages RecordedMethodUsages, CancellationToken CancellationToken);
}