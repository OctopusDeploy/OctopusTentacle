using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Gateway;
using Octopus.Pipelines.Runner.Kubernetes;
using Octopus.Pipelines.Runner.Logs.AzureBlob;

namespace Octopus.Pipelines.Runner.Kubernetes.Host;

/// <summary>Composes the AKS host's services. Separate from <c>Program</c> so tests can compose without running.</summary>
internal static class RunnerHost
{
    internal static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var runnerSection = configuration.GetSection(RunnerOptions.Section);
        services.AddSingleton(TimeProvider.System);
        services.AddRunnerGateway(runnerSection);

        services.AddKubernetesBackend(runnerSection.GetSection("Kubernetes"));

        // Job logs only go to the storage cell's blob containers, never to the runner's own disk.
        services.AddSingleton<ILogStore>(new LogStores(new Dictionary<string, ILogStore> { ["https"] = new AzureBlobLogStore() }));
    }
}
