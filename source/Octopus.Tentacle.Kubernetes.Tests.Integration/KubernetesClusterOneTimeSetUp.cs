using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;
using Octopus.Tentacle.Util;

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

        installer = new KubernetesClusterInstaller(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, kindExePath, helmExePath, kubeCtlPath, KubernetesTestsGlobalContext.Instance.Logger);
        await installer.Install();

        //if we are not running in TeamCity, then we need to find the latest local tag and use that if it exists 
        if (!TeamCityDetection.IsRunningInTeamCity() && bool.TryParse(Environment.GetEnvironmentVariable("KubernetesAgentTests_UseLocalImage"), out var useLocal) && useLocal)
        {
            var imageLoader = new DockerImageLoader(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, KubernetesTestsGlobalContext.Instance.Logger, kindExePath);
            KubernetesTestsGlobalContext.Instance.TentacleImageAndTag = imageLoader.LoadMostRecentImageIntoKind(installer.ClusterName);
        }
        else if(TeamCityDetection.IsRunningInTeamCity())
        {
            var tag = Environment.GetEnvironmentVariable("KubernetesAgentTests_ImageTag");
            KubernetesTestsGlobalContext.Instance.TentacleImageAndTag = $"docker.packages.octopushq.com/octopusdeploy/kubernetes-tentacle:{tag}";
        }

        if (KubernetesTestsGlobalContext.Instance.TentacleImageAndTag is not null)
        {
            KubernetesTestsGlobalContext.Instance.Logger.Information("Using tentacle image: {ImageAndTag}", KubernetesTestsGlobalContext.Instance.TentacleImageAndTag);
        }

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