using System;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.KubernetesAgent;

[SetUpFixture]
public class KubernetesClusterOneTimeSetUp
{
    KubernetesClusterInstaller? installer;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var toolDownloader = new RequiredToolDownloader(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, KubernetesTestsGlobalContext.Instance.Logger);
        var (kindExePath, helmExePath, kubeCtlPath) = await toolDownloader.DownloadRequiredTools(CancellationToken.None);

        installer = new KubernetesClusterInstaller(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, kindExePath, helmExePath, kubeCtlPath, KubernetesTestsGlobalContext.Instance.Logger);
        await installer.InstallLatestSupported();

        KubernetesTestsGlobalContext.Instance.TentacleImageAndTag = SetupHelpers.GetTentacleImageAndTag(kindExePath, installer);
        KubernetesTestsGlobalContext.Instance.SetToolExePaths(helmExePath, kubeCtlPath);
        KubernetesTestsGlobalContext.Instance.KubeConfigPath = installer.KubeConfigPath;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        installer?.Dispose();
        KubernetesTestsGlobalContext.Instance.Dispose();
    }
}