using System;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;
using Octopus.Tentacle.Util;

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
        await installer.Install();

        KubernetesTestsGlobalContext.Instance.TentacleImageAndTag = GetTentacleImageAndTag(kindExePath);
        KubernetesTestsGlobalContext.Instance.SetToolExePaths(helmExePath, kubeCtlPath);
        KubernetesTestsGlobalContext.Instance.KubeConfigPath = installer.KubeConfigPath;
    }

    string? GetTentacleImageAndTag(string kindExePath)
    {
        if (installer == null)
        {
            throw new InvalidOperationException("Expected installer to be set");
        }
        //By default, we don't override the values in the helm chart. This is useful if you are just writing new tests and not changing Tentacle code.
        string? imageAndTag = null;
        if (TeamCityDetection.IsRunningInTeamCity())
        {
            //In TeamCity, use the tag of the currently building code
            var tag = Environment.GetEnvironmentVariable("KubernetesAgentTests_ImageTag");
            imageAndTag = $"docker.packages.octopushq.com/octopusdeploy/kubernetes-agent-tentacle:{tag}";
        }
        else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KubernetesAgentTests_ImageAndTag")))
        {
            imageAndTag = Environment.GetEnvironmentVariable("KubernetesAgentTests_ImageAndTag");
        }
        else if(bool.TryParse(Environment.GetEnvironmentVariable("KubernetesAgentTests_UseLatestLocalImage"), out var useLocal) && useLocal)
        {
            //if we should use the latest locally build image, load the tag from docker and load it into kind
            var imageLoader = new DockerImageLoader(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, KubernetesTestsGlobalContext.Instance.Logger, kindExePath);
            imageAndTag = imageLoader.LoadMostRecentImageIntoKind(installer.ClusterName);
        }
        
        if(imageAndTag is not null)
            KubernetesTestsGlobalContext.Instance.Logger.Information("Using tentacle image: {ImageAndTag}", imageAndTag);

        return imageAndTag;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        installer?.Dispose();
        KubernetesTestsGlobalContext.Instance.Dispose();
    }
}