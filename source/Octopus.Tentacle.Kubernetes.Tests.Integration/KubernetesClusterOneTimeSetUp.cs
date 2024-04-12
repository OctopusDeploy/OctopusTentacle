using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[SetUpFixture]
public class KubernetesClusterOneTimeSetUp
{
    readonly KubernetesClusterInstaller installer = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await installer.Install();

        TestKubernetesCluster.KubeConfigPath = installer.KubeConfigPath;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        installer?.Dispose();
    }
}