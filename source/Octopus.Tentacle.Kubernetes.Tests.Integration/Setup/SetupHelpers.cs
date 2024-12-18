using Halibut;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Common.Logging;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

static class SetupHelpers
{
    public static HalibutRuntime BuildServerHalibutRuntime()
    {
        var serverHalibutRuntimeBuilder = new HalibutRuntimeBuilder()
            .WithServerCertificate(TestCertificates.Server)
            .WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits.RecommendedValues())
            .WithLogFactory(new TestContextLogCreator("Server", LogLevel.Trace).ToCachingLogFactory());

        return serverHalibutRuntimeBuilder.Build();
    }
    
    public static TentacleClient BuildTentacleClient(Uri uri, string? thumbprint, HalibutRuntime halibutRuntime, Action<TentacleServiceDecoratorBuilder> tentacleServiceDecoratorBuilderAction)
    {
        var endpoint = new ServiceEndPoint(uri, thumbprint, halibutRuntime.TimeoutsAndLimits);

        var retrySettings = new RpcRetrySettings(true, TimeSpan.FromMinutes(2));
        var clientOptions = new TentacleClientOptions(retrySettings);

        TentacleClient.CacheServiceWasNotFoundResponseMessages(halibutRuntime);

        var builder = new TentacleServiceDecoratorBuilder();
        tentacleServiceDecoratorBuilderAction(builder);

        return new TentacleClient(
            endpoint,
            halibutRuntime,
            new PollingTentacleScriptObserverBackoffStrategy(),
            new NoTentacleClientObserver(),
            clientOptions,
            builder.Build());
    }
    
    public static string? GetTentacleImageAndTag(string kindExePath, KubernetesClusterInstaller clusterInstaller)
    {
        if (clusterInstaller == null)
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
            imageAndTag = imageLoader.LoadMostRecentImageIntoKind(clusterInstaller.ClusterName);
        }
        
        if(imageAndTag is not null)
            KubernetesTestsGlobalContext.Instance.Logger.Information("Using tentacle image: {ImageAndTag}", imageAndTag);

        return imageAndTag;
    }
}