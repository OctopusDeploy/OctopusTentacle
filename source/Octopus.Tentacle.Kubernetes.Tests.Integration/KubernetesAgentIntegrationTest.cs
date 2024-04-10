using Halibut;
using Halibut.Diagnostics;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public abstract class KubernetesAgentIntegrationTest
{
    readonly KubernetesAgentInstaller kubernetesAgentInstaller = new();
    protected ILogger Logger { get; }

    protected HalibutRuntime ServerHalibutRuntime { get; private set; } = null!;

    protected TentacleClient TentacleClient { get; private set; } = null!;

    protected KubernetesAgentIntegrationTest() 
    {
        Logger = new LoggerConfiguration()
            .WriteTo.NUnitOutput()
            .WriteTo.Console()
            .CreateLogger()
            .ForContext<KubernetesAgentIntegrationTest>();
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await kubernetesAgentInstaller.DownloadHelm();

        //create a new server halibut runtime
        var listeningPort = BuildServerHalibutRuntimeAndListen();
        
        await kubernetesAgentInstaller.InstallAgent(TestKubernetesCluster.KubeConfigPath, listeningPort);

        BuildTentacleClient();
    }

    void BuildTentacleClient()
    {
        var endpoint = new ServiceEndPoint(kubernetesAgentInstaller.SubscriptionId, TestCertificates.TentaclePublicThumbprint, ServerHalibutRuntime.TimeoutsAndLimits);
        
        var retrySettings = new RpcRetrySettings(true, TimeSpan.FromMinutes(2));
        var clientOptions = new TentacleClientOptions(retrySettings);

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
            .WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits.RecommendedValues());

        ServerHalibutRuntime = serverHalibutRuntimeBuilder.Build();

        ServerHalibutRuntime.Trust(TestCertificates.TentaclePublicThumbprint);
        return ServerHalibutRuntime.Listen();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await ServerHalibutRuntime.DisposeAsync();
        kubernetesAgentInstaller?.Dispose();
    }
}