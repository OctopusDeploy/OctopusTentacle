using Halibut;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Support.Logging;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public abstract class KubernetesAgentIntegrationTest
{
    KubernetesAgentInstaller kubernetesAgentInstaller;
    TraceLogFileLogger? traceLogFileLogger;
    CancellationTokenSource cancellationTokenSource;
    protected ILogger Logger { get; private set; }

    protected HalibutRuntime ServerHalibutRuntime { get; private set; } = null!;

    protected TentacleClient TentacleClient { get; private set; } = null!;

    protected CancellationToken CancellationToken { get; private set; }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        kubernetesAgentInstaller = new KubernetesAgentInstaller(
            KubernetesTestsGlobalContext.Instance.TemporaryDirectory,
            KubernetesTestsGlobalContext.Instance.HelmExePath,
            KubernetesTestsGlobalContext.Instance.KubeCtlExePath,
            KubernetesTestsGlobalContext.Instance.KubeConfigPath,
            KubernetesTestsGlobalContext.Instance.Logger);
        
        //create a new server halibut runtime
        var listeningPort = BuildServerHalibutRuntimeAndListen();
        
        var thumbprint = await kubernetesAgentInstaller.InstallAgent(listeningPort);
        
        
        //trust the generated cert thumbprint
        ServerHalibutRuntime.Trust(thumbprint);

        BuildTentacleClient();
    }

    [SetUp]
    public void SetUp()
    {
        traceLogFileLogger = new TraceLogFileLogger(SerilogLoggerBuilder.CurrentTestHash());
        Logger = new SerilogLoggerBuilder()
            .SetTraceLogFileLogger(traceLogFileLogger)
            .Build()
            .ForContext(GetType());
        
        cancellationTokenSource = new CancellationTokenSource();
        CancellationToken = cancellationTokenSource.Token;
    }

    [TearDown]
    public async Task TearDown()
    {
        if (traceLogFileLogger is not null)
        {
           await traceLogFileLogger.DisposeAsync();
        }
    }

    void BuildTentacleClient()
    {
        var endpoint = new ServiceEndPoint(kubernetesAgentInstaller.SubscriptionId, TestCertificates.TentaclePublicThumbprint, ServerHalibutRuntime.TimeoutsAndLimits);
        
        var retrySettings = new RpcRetrySettings(true, TimeSpan.FromMinutes(2));
        var clientOptions = new TentacleClientOptions(retrySettings);
        
        TentacleClient.CacheServiceWasNotFoundResponseMessages(ServerHalibutRuntime);

        TentacleClient = new TentacleClient(
            endpoint,
            ServerHalibutRuntime,
            new PollingTentacleScriptObserverBackoffStrategy(),
            new NoTentacleClientObserver(),
            clientOptions);
    }

    int BuildServerHalibutRuntimeAndListen()
    {
        var serverHalibutRuntimeBuilder = new HalibutRuntimeBuilder()
            .WithServerCertificate(TestCertificates.Server)
            .WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits.RecommendedValues())
            .WithLogFactory(new TestContextLogCreator("Server", LogLevel.Trace).ToCachingLogFactory());

        ServerHalibutRuntime = serverHalibutRuntimeBuilder.Build();

        return ServerHalibutRuntime.Listen();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await ServerHalibutRuntime.DisposeAsync();
        kubernetesAgentInstaller?.Dispose();
    }
}