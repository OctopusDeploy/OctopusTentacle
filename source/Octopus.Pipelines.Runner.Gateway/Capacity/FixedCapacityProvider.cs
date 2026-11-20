using Microsoft.Extensions.Options;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// Keeps at most <see cref="GatewayOptions.MaxConcurrentJobs"/> jobs in flight, whatever the cluster is doing,
/// and at most <see cref="GatewayOptions.MaxStartingJobs"/> of them not yet started (when that's above 0).
/// </summary>
public sealed class FixedCapacityProvider(GatewayJobExecutor executor, IOptions<GatewayOptions> options) : ICapacityProvider
{
    public ValueTask<int> GetAvailableAsync(CancellationToken cancellationToken)
    {
        var runner = options.Value;
        var available = runner.MaxConcurrentJobs - executor.InFlightCount;
        if (runner.MaxStartingJobs > 0)
        {
            available = Math.Min(available, runner.MaxStartingJobs - executor.StartingCount);
        }

        return ValueTask.FromResult(available);
    }
}
