using Halibut;
using Halibut.Diagnostics;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public abstract class KubernetesAgentIntegrationTest
{
    readonly KubernetesAgentInstaller kubernetesAgentInstaller = new();
    protected ILogger Logger { get; }

    protected HalibutRuntime ServerHalibutRuntime { get; private set; } = null!;

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

        var listeningPort = BuildServerHalibutRuntimeAndListen();
        
        await kubernetesAgentInstaller.InstallAgent(TestKubernetesCluster.KubeConfigPath, listeningPort);
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
    public void OneTimeTearDown()
    {
        kubernetesAgentInstaller?.Dispose();
    }
}