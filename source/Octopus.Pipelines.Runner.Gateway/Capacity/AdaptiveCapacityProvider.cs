using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// Like <see cref="FixedCapacityProvider"/>, but finds the starting cap itself from how long this runner's
/// jobs take to start (<see cref="AdaptiveStartLimit"/>). It needs no cluster metrics or extra permissions,
/// and works whatever is slow: the CNI, the kubelet, image pulls or the scheduler. Each runner tunes its
/// own limit; like TCP flows, runners sharing a cell settle on roughly equal shares.
/// </summary>
public sealed class AdaptiveCapacityProvider : ICapacityProvider
{
    readonly GatewayJobExecutor executor;
    readonly IOptions<GatewayOptions> options;
    readonly TimeProvider clock;
    readonly ILogger<AdaptiveCapacityProvider> logger;
    readonly AdaptiveStartLimit limit;
    readonly long origin;

    public AdaptiveCapacityProvider(GatewayJobExecutor executor, IOptions<GatewayOptions> options, TimeProvider clock, ILogger<AdaptiveCapacityProvider> logger)
    {
        this.executor = executor;
        this.options = options;
        this.clock = clock;
        this.logger = logger;
        origin = clock.GetTimestamp();

        var runner = options.Value;
        var ceiling = runner.MaxStartingJobs > 0 ? runner.MaxStartingJobs : runner.MaxConcurrentJobs;
        limit = new AdaptiveStartLimit(runner.AdaptiveStarts, ceiling);
        executor.JobStartObserved += (assigned, started) => limit.RecordStart(At(assigned), At(started));
    }

    /// <summary>The current starting cap.</summary>
    public int Limit => limit.Limit;

    public ValueTask<int> GetAvailableAsync(CancellationToken cancellationToken)
    {
        var before = limit.Limit;
        if (limit.Adjust(At(clock.GetTimestamp()), executor.StartingSince.Select(At).ToArray()))
        {
            var after = limit.Limit;
            logger.Log(after < before ? LogLevel.Information : LogLevel.Debug,
                "Starting cap {Before} -> {After} (p90 start {P90:0.0}s, target {Target:0.0}s, {Starting} starting, {InFlight} in flight)",
                before, after, limit.LastPercentile90?.TotalSeconds, limit.Target.TotalSeconds, executor.StartingCount, executor.InFlightCount);
        }

        var byConcurrency = options.Value.MaxConcurrentJobs - executor.InFlightCount;
        var byStarting = limit.Limit - executor.StartingCount;
        if (byStarting <= 0 && byConcurrency > 0)
        {
            limit.MarkLimited();
        }

        return ValueTask.FromResult(Math.Min(byConcurrency, byStarting));
    }

    TimeSpan At(long timestamp) => clock.GetElapsedTime(origin, timestamp);
}
