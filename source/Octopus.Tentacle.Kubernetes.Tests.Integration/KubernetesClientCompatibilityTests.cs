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
        new object[] {new ClusterVersion(1, 33)},
        new object[] {new ClusterVersion(1, 32)},
    ];

    KubernetesTestsGlobalContext? testContext;
    ILogger logger = null!;
    TemporaryDirectory toolsTemporaryDirectory;
    string kindExePath;
    string helmExePath;
    string kubeCtlPath;
    KubernetesClusterInstaller? clusterInstaller;
    KubernetesAgentInstaller? kubernetesAgentInstaller;
    HalibutRuntime serverHalibutRuntime = null!;
    string? agentThumbprint;
    TraceLogFileLogger? traceLogFileLogger;
    CancellationToken cancellationToken;
    CancellationTokenSource? cancellationTokenSource;
    TentacleClient tentacleClient = null!;
    IRecordedMethodUsages recordedMethodUsages = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        logger = new SerilogLoggerBuilder().Build();
        toolsTemporaryDirectory = new TemporaryDirectory();
        var toolDownloader = new RequiredToolDownloader(toolsTemporaryDirectory, logger);
        (kindExePath, helmExePath, kubeCtlPath) = await toolDownloader.DownloadRequiredTools(CancellationToken.None);
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
        await SetUp(clusterVersion);
        
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
    
    async Task SetUp(ClusterVersion clusterVersion)
    {
        testContext = new KubernetesTestsGlobalContext(logger);
        
        await SetupCluster(clusterVersion);

        kubernetesAgentInstaller = new KubernetesAgentInstaller(
            testContext.TemporaryDirectory,
            testContext.HelmExePath,
            testContext.KubeCtlExePath,
            testContext.KubeConfigPath,
            testContext.Logger);

        //create a new server halibut runtime
        serverHalibutRuntime = SetupHelpers.BuildServerHalibutRuntime();
        var listeningPort = serverHalibutRuntime.Listen();

        agentThumbprint = await kubernetesAgentInstaller.InstallAgent(listeningPort, testContext.TentacleImageAndTag, new Dictionary<string, string>());

        //trust the generated cert thumbprint
        serverHalibutRuntime.Trust(agentThumbprint);

        traceLogFileLogger = new TraceLogFileLogger(LoggingUtils.CurrentTestHash());
        logger = new SerilogLoggerBuilder()
            .SetTraceLogFileLogger(traceLogFileLogger)
            .Build()
            .ForContext(GetType());

        cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));
        cancellationToken = cancellationTokenSource.Token;

        tentacleClient = SetupHelpers.BuildTentacleClient(kubernetesAgentInstaller.SubscriptionId, agentThumbprint, serverHalibutRuntime, builder =>
        {
            builder.RecordMethodUsages<IAsyncClientKubernetesScriptServiceV1>(out var recordedUsages);
            recordedMethodUsages = recordedUsages;
        });
    }
    
    async Task SetupCluster(ClusterVersion clusterVersion)
    {
        clusterInstaller = new KubernetesClusterInstaller(testContext.TemporaryDirectory, kindExePath, helmExePath, kubeCtlPath, testContext.Logger);
        await clusterInstaller.Install(clusterVersion);

        testContext.TentacleImageAndTag = SetupHelpers.GetTentacleImageAndTag(kindExePath, clusterInstaller);
        testContext.SetToolExePaths(helmExePath, kubeCtlPath);
        testContext.KubeConfigPath = clusterInstaller.KubeConfigPath;
    }
}