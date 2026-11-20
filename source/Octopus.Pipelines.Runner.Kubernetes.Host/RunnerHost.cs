using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octopus.Pipelines.Runner.Core;
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
        services.Configure<RunnerOptions>(runnerSection);
        services.AddSingleton(TimeProvider.System);
        services.AddRunnerGateway(runnerSection);

        var backend = runnerSection.GetValue(nameof(RunnerOptions.Backend), JobBackendKind.Kubernetes);
        var logStores = new Dictionary<string, ILogStore>
        {
            ["https"] = new AzureBlobLogStore(),
        };
        switch (backend)
        {
            case JobBackendKind.Kubernetes:
                services.AddKubernetesBackend(runnerSection.GetSection("Kubernetes"));
                break;
            case JobBackendKind.Process:
                services.AddSingleton<IJobBackend, ProcessJobBackend>();
                // Only the Process backend (local development) writes job logs to local disk.
                // Falls back to the root scripts/run-local.sh gives the local Server's storage cell.
                var logRoot = runnerSection["LogRoot"]
                    ?? Path.Combine(Path.GetTempPath(), "targetscalepoc-local", "storage-cell");
                logStores["file"] = new FileLogStore(logRoot);
                break;
        }

        services.AddSingleton<ILogStore>(new LogStores(logStores));
    }
}
