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
        new object[] {new ClusterVersion(1, 31)},
        new object[] {new ClusterVersion(1, 30)},
        new object[] {new ClusterVersion(1, 29)},
        new object[] {new ClusterVersion(1, 28)},
    ];
    
    string kindExePath;
    string helmExePath;
    string kubeCtlPath;
    KubernetesClusterInstaller clusterInstaller = null!;
    KubernetesAgentInstaller? kubernetesAgentInstaller;
    HalibutRuntime serverHalibutRuntime = null!;
    string? agentThumbprint;
    TraceLogFileLogger? traceLogFileLogger;
    CancellationToken cancellationToken;
    CancellationTokenSource? cancellationTokenSource;
    ILogger? logger;
    TentacleClient tentacleClient = null!;
    IRecordedMethodUsages recordedMethodUsages = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        var toolDownloader = new RequiredToolDownloader(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, KubernetesTestsGlobalContext.Instance.Logger);
        (kindExePath, helmExePath, kubeCtlPath) = await toolDownloader.DownloadRequiredTools(CancellationToken.None);
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

        clusterInstaller.Dispose();
        KubernetesTestsGlobalContext.Instance.Dispose();
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
        await SetupCluster(clusterVersion);

        kubernetesAgentInstaller = new KubernetesAgentInstaller(
            KubernetesTestsGlobalContext.Instance.TemporaryDirectory,
            KubernetesTestsGlobalContext.Instance.HelmExePath,
            KubernetesTestsGlobalContext.Instance.KubeCtlExePath,
            KubernetesTestsGlobalContext.Instance.KubeConfigPath,
            KubernetesTestsGlobalContext.Instance.Logger);

        //create a new server halibut runtime
        serverHalibutRuntime = SetupHelpers.BuildServerHalibutRuntime();
        var listeningPort = serverHalibutRuntime.Listen();

        agentThumbprint = await kubernetesAgentInstaller.InstallAgent(listeningPort, KubernetesTestsGlobalContext.Instance.TentacleImageAndTag, new Dictionary<string, string>());

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
        clusterInstaller = new KubernetesClusterInstaller(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, kindExePath, helmExePath, kubeCtlPath, KubernetesTestsGlobalContext.Instance.Logger);
        await clusterInstaller.Install(clusterVersion);

        KubernetesTestsGlobalContext.Instance.TentacleImageAndTag = SetupHelpers.GetTentacleImageAndTag(kindExePath, clusterInstaller);
        KubernetesTestsGlobalContext.Instance.SetToolExePaths(helmExePath, kubeCtlPath);
        KubernetesTestsGlobalContext.Instance.KubeConfigPath = clusterInstaller.KubeConfigPath;
    }
}