using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Octopus.Pipelines.Runner.Core;

public static class RunnerCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="JobRunner"/> and its <see cref="JobRunnerOptions"/>. The caller registers an
    /// <see cref="IJobBackend"/> and an <see cref="Logs.ILogStore"/>. Constructing a runner with <c>new</c> works too.
    /// </summary>
    public static IServiceCollection AddRunnerCore(this IServiceCollection services, Action<JobRunnerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new JobRunnerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        // Passes the logger explicitly: JobRunner takes a non-generic ILogger, which the container can't supply.
        services.AddSingleton(sp => new JobRunner(
            sp.GetRequiredService<IJobBackend>(),
            sp.GetRequiredService<Logs.ILogStore>(),
            sp.GetRequiredService<JobRunnerOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<ILogger<JobRunner>>()));
        return services;
    }
}
