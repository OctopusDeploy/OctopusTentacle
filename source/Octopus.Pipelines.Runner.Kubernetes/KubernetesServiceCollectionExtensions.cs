using k8s;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octopus.Pipelines.Runner.Core;

namespace Octopus.Pipelines.Runner.Kubernetes;

public static class KubernetesServiceCollectionExtensions
{
    /// <summary>
    /// Runs each job as a bare pod. <paramref name="kubernetesSection"/> is <c>Runner:Kubernetes</c>; it binds
    /// <see cref="KubernetesBackendOptions"/>. The client is created on first use, from the in-cluster
    /// configuration or else the kubeconfig. Needs <c>GatewayOptions</c> (see <c>AddRunnerGateway</c>) for the runner's identity.
    /// </summary>
    public static IServiceCollection AddKubernetesBackend(this IServiceCollection services, IConfiguration kubernetesSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(kubernetesSection);

        services.Configure<KubernetesBackendOptions>(kubernetesSection);
        services.AddSingleton<IKubernetes>(_ => new k8s.Kubernetes(
            KubernetesClientConfiguration.IsInCluster()
                ? KubernetesClientConfiguration.InClusterConfig()
                : KubernetesClientConfiguration.BuildConfigFromConfigFile()));
        services.AddSingleton<PodWatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<PodWatcher>());
        services.AddSingleton<IJobBackend, KubernetesJobBackend>();
        return services;
    }
}
