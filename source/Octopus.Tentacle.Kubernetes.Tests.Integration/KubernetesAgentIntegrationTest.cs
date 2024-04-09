using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public abstract class KubernetesAgentIntegrationTest
{
    readonly KubernetesAgentInstaller kubernetesAgentInstaller = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await kubernetesAgentInstaller.DownloadHelm();

        await kubernetesAgentInstaller.InstallAgent(TestKubernetesCluster.KubeConfigPath);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        kubernetesAgentInstaller?.Dispose();
    }
}