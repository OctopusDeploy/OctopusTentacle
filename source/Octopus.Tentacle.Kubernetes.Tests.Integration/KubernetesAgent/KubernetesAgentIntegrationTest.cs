using System;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Tooling;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.KubernetesAgent;

public abstract class KubernetesAgentIntegrationTest
{
    KubernetesAgentInstaller? kubernetesAgentInstaller;
    KubeCtlTool? kubeCtl;
    TraceLogFileLogger? traceLogFileLogger;
    CancellationTokenSource? cancellationTokenSource;
    protected ILogger? Logger { get; private set; }
    
    protected KubernetesAgentInstaller KubernetesAgentInstaller => kubernetesAgentInstaller ?? throw new InvalidOperationException("Expected kubernetesAgentInstaller to be set");
    
    protected TentacleClient TentacleClient { get; private set; } = null!;

    protected CancellationToken CancellationToken { get; private set; }
    
    protected KubeCtlTool KubeCtl => kubeCtl ?? throw new InvalidOperationException("Expected kubeCtl to be set");

    protected readonly IDictionary<string, string> CustomHelmValues = new Dictionary<string, string>();

    HalibutRuntime serverHalibutRuntime;

    string? agentThumbprint;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        kubernetesAgentInstaller = new KubernetesAgentInstaller(
            KubernetesTestsGlobalContext.Instance.TemporaryDirectory,
            KubernetesTestsGlobalContext.Instance.HelmExePath,
            KubernetesTestsGlobalContext.Instance.KubeCtlExePath,
            KubernetesTestsGlobalContext.Instance.KubeConfigPath,
            KubernetesTestsGlobalContext.Instance.Logger);
        
        kubeCtl = new KubeCtlTool(
            KubernetesTestsGlobalContext.Instance.TemporaryDirectory,
            KubernetesTestsGlobalContext.Instance.KubeCtlExePath,
            KubernetesTestsGlobalContext.Instance.KubeConfigPath,
            kubernetesAgentInstaller.Namespace,
            KubernetesTestsGlobalContext.Instance.Logger);

        //create a new server halibut runtime
        serverHalibutRuntime = SetupHelpers.BuildServerHalibutRuntime();
        var listeningPort = serverHalibutRuntime.Listen();

        agentThumbprint = await kubernetesAgentInstaller.InstallAgent(listeningPort, KubernetesTestsGlobalContext.Instance.TentacleImageAndTag, CustomHelmValues);

        //trust the generated cert thumbprint
        serverHalibutRuntime.Trust(agentThumbprint);
    }

    [SetUp]
    public void SetUp()
    {
        traceLogFileLogger = new TraceLogFileLogger(LoggingUtils.CurrentTestHash());
        Logger = new SerilogLoggerBuilder()
            .SetTraceLogFileLogger(traceLogFileLogger)
            .Build()
            .ForContext(GetType());

        cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));
        CancellationToken = cancellationTokenSource.Token;

        //each test should get its own tentacle client, so it gets its own builders
        TentacleClient = SetupHelpers.BuildTentacleClient(KubernetesAgentInstaller.SubscriptionId, agentThumbprint, serverHalibutRuntime, ConfigureTentacleServiceDecoratorBuilder);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (traceLogFileLogger is not null)
        {
            await traceLogFileLogger.DisposeAsync();
        }

        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }

    protected virtual void ConfigureTentacleServiceDecoratorBuilder(TentacleServiceDecoratorBuilder builder)
    {
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await serverHalibutRuntime.DisposeAsync();
        kubernetesAgentInstaller?.Dispose();
    }
}