using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[SetUpFixture]
public class KubernetesClusterOneTimeSetUp
{
    KubernetesClusterInstaller installer;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var toolDownloader = new RequiredToolDownloader(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, KubernetesTestsGlobalContext.Instance.Logger);
        var (kindExePath, helmExePath, kubeCtlPath) = await toolDownloader.DownloadRequiredTools(CancellationToken.None);
        
        installer = new KubernetesClusterInstaller(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, kindExePath, helmExePath, kubeCtlPath);
        await installer.Install();

        KubernetesTestsGlobalContext.Instance.SetToolExePaths(helmExePath, kubeCtlPath);
        KubernetesTestsGlobalContext.Instance.KubeConfigPath = installer.KubeConfigPath;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        installer.Dispose();
        KubernetesTestsGlobalContext.Instance.Dispose();
    }
}