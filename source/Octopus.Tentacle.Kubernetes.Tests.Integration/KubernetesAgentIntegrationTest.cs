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
using Octopus.Tentacle.Kubernetes.Tests.Integration.Tooling;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public abstract class KubernetesAgentIntegrationTest
{
    protected KubernetesAgentInstaller kubernetesAgentInstaller;
    TraceLogFileLogger? traceLogFileLogger;
    CancellationTokenSource cancellationTokenSource;
    protected ILogger Logger { get; private set; }

    protected HalibutRuntime ServerHalibutRuntime { get; private set; } = null!;

    protected TentacleClient TentacleClient { get; private set; } = null!;

    protected CancellationToken CancellationToken { get; private set; }
    
    protected KubeCtlTool KubeCtl { get; private set; }

    protected IDictionary<string, string>? CustomHelmValues = new Dictionary<string, string>();

    string agentThumbprint;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        kubernetesAgentInstaller = new KubernetesAgentInstaller(
            KubernetesTestsGlobalContext.Instance.TemporaryDirectory,
            KubernetesTestsGlobalContext.Instance.HelmExePath,
            KubernetesTestsGlobalContext.Instance.KubeCtlExePath,
            KubernetesTestsGlobalContext.Instance.KubeConfigPath,
            KubernetesTestsGlobalContext.Instance.Logger);
        
        KubeCtl = new KubeCtlTool(
            KubernetesTestsGlobalContext.Instance.TemporaryDirectory,
            KubernetesTestsGlobalContext.Instance.KubeCtlExePath,
            KubernetesTestsGlobalContext.Instance.KubeConfigPath,
            kubernetesAgentInstaller.Namespace,
            KubernetesTestsGlobalContext.Instance.Logger);

        //create a new server halibut runtime
        var listeningPort = BuildServerHalibutRuntimeAndListen();

        agentThumbprint = await kubernetesAgentInstaller.InstallAgent(listeningPort, KubernetesTestsGlobalContext.Instance.TentacleImageAndTag, CustomHelmValues);

        //trust the generated cert thumbprint
        ServerHalibutRuntime.Trust(agentThumbprint);
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
        BuildTentacleClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (traceLogFileLogger is not null)
        {
            await traceLogFileLogger.DisposeAsync();
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    protected virtual TentacleServiceDecoratorBuilder ConfigureTentacleServiceDecoratorBuilder(TentacleServiceDecoratorBuilder builder) => builder;

    void BuildTentacleClient()
    {
        var endpoint = new ServiceEndPoint(kubernetesAgentInstaller.SubscriptionId, agentThumbprint, ServerHalibutRuntime.TimeoutsAndLimits);

        var retrySettings = new RpcRetrySettings(true, TimeSpan.FromMinutes(2));
        var clientOptions = new TentacleClientOptions(retrySettings);

        TentacleClient.CacheServiceWasNotFoundResponseMessages(ServerHalibutRuntime);

        var builder = new TentacleServiceDecoratorBuilder();
        ConfigureTentacleServiceDecoratorBuilder(builder);

        TentacleClient = new TentacleClient(
            endpoint,
            ServerHalibutRuntime,
            new PollingTentacleScriptObserverBackoffStrategy(),
            new NoTentacleClientObserver(),
            clientOptions,
            builder.Build());
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