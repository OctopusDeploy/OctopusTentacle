using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Core;

namespace Octopus.Pipelines.Runner.Gateway;

public static class RunnerGatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the gateway loop: pulls jobs from the Server cell, runs each with <see cref="JobRunner"/> and
    /// reports back through the <see cref="Outbox"/>. <paramref name="runnerSection"/> is the <c>Runner</c> section;
    /// it binds <see cref="GatewayOptions"/> and the log settings of <see cref="JobRunnerOptions"/>. The host
    /// registers an <see cref="IJobBackend"/> and an <see cref="Core.Logs.ILogStore"/>.
    /// </summary>
    public static IServiceCollection AddRunnerGateway(this IServiceCollection services, IConfiguration runnerSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(runnerSection);

        services.Configure<GatewayOptions>(runnerSection);
        services.AddRunnerCore(options =>
        {
            options.LogFlushInterval = runnerSection.GetValue(nameof(JobRunnerOptions.LogFlushInterval), options.LogFlushInterval);
            options.LogFlushBytes = runnerSection.GetValue(nameof(JobRunnerOptions.LogFlushBytes), options.LogFlushBytes);
            options.MaxPendingLogBytes = runnerSection.GetValue(nameof(JobRunnerOptions.MaxPendingLogBytes), options.MaxPendingLogBytes);
        });
        services.AddSingleton<Outbox>();
        services.AddSingleton<CapacitySignal>();
        services.AddSingleton<GatewayJobExecutor>();
        if (runnerSection.GetSection(nameof(GatewayOptions.AdaptiveStarts)).GetValue<bool>(nameof(AdaptiveStartOptions.Enabled)))
        {
            services.AddSingleton<ICapacityProvider, AdaptiveCapacityProvider>();
        }
        else
        {
            services.AddSingleton<ICapacityProvider, FixedCapacityProvider>();
        }

        // The container disposes the channel after the client, when the host shuts down.
        services.AddSingleton(sp => GatewayChannel.Create(sp.GetRequiredService<IOptions<GatewayOptions>>().Value));
        services.AddSingleton<CallInvoker>(sp => sp.GetRequiredService<GrpcChannel>().CreateCallInvoker());
        services.AddHostedService<GatewayClient>();
        return services;
    }
}
