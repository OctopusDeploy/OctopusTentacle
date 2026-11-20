using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octopus.Pipelines.Runner.Kubernetes.Host;
using Octopus.Pipelines.Runner.Core.Logs;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Composes the AKS host's services without running it, and checks what each backend can write to.</summary>
public sealed class HostCompositionTests
{
    static ServiceProvider ComposeFrom(Dictionary<string, string?> settings) => Compose(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    static ServiceProvider Compose(IConfiguration configuration)
    {
        // A host builder supplies what hosted services need (lifetime, logging); nothing here starts it.
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddLogging();
        RunnerHost.ConfigureServices(builder.Services, configuration);
        // The capacity provider's dependencies reach the backend, so give it a client that never connects.
        builder.Services.AddSingleton<k8s.IKubernetes>(_ => new k8s.Kubernetes(new k8s.KubernetesClientConfiguration { Host = "http://localhost:1" }));
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public async Task Kubernetes_backend_refuses_file_log_targets()
    {
        using var provider = ComposeFrom(new() { ["Runner:Backend"] = "Kubernetes" });
        var store = provider.GetRequiredService<ILogStore>();

        await Assert.ThrowsAsync<NotSupportedException>(() => store.CreateAsync(new Uri("file:///tmp/x"), "job.log", CancellationToken.None));
    }

    [Fact]
    public async Task Process_backend_accepts_file_targets_under_the_log_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "hostcomposition-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = ComposeFrom(new() { ["Runner:Backend"] = "Process", ["Runner:LogRoot"] = root });
            var store = provider.GetRequiredService<ILogStore>();

            await using var log = await store.CreateAsync(new Uri(Path.Combine(root, "job-1")), "job.log", CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(root, "job-1", "job.log")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
