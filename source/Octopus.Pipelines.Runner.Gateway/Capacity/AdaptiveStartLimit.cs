namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// The starting cap for <see cref="AdaptiveCapacityProvider"/>, adjusted from pod start delays in the
/// style of TCP congestion control (see <see cref="AdaptiveStartOptions"/>). Times are offsets from any
/// fixed origin, so this stays free of clocks and easy to test. Thread-safe.
/// </summary>
public sealed class AdaptiveStartLimit
{
    readonly AdaptiveStartOptions options;
    readonly int ceiling;
    readonly Lock gate = new();
    readonly List<TimeSpan> delays = [];

    /// <summary>Each recent interval's 10th percentile start delay, for the cell's normal start time.</summary>
    readonly Queue<(TimeSpan At, TimeSpan Fast)> baselines = new();

    double limit;
    bool limited;
    bool slowStart = true;
    TimeSpan lastAdjust;
    TimeSpan lastBackOff = TimeSpan.MinValue;
    TimeSpan? baseline;

    public AdaptiveStartLimit(AdaptiveStartOptions options, int ceiling, TimeSpan now = default)
    {
        this.options = options;
        this.ceiling = Math.Max(1, ceiling);
        limit = Math.Clamp(options.InitialLimit, Floor, this.ceiling);
        lastAdjust = now;
    }

    int Floor => Math.Clamp(options.MinLimit, 1, ceiling);

    public int Limit
    {
        get
        {
            lock (gate)
            {
                return (int)limit;
            }
        }
    }

    /// <summary>The start delay above which the cell counts as congested.</summary>
    public TimeSpan Target
    {
        get
        {
            lock (gate)
            {
                return CurrentTarget;
            }
        }
    }

    /// <summary>The 90th percentile start delay at the last adjustment, if there were any samples.</summary>
    public TimeSpan? LastPercentile90 { get; private set; }

    TimeSpan CurrentTarget => baseline is { } normal && normal * options.BaselineMultiplier > options.TargetStartDelay
        ? normal * options.BaselineMultiplier
        : options.TargetStartDelay;

    /// <summary>A job started. Only jobs assigned since the last back-off say anything about the current limit.</summary>
    public void RecordStart(TimeSpan assignedAt, TimeSpan startedAt)
    {
        lock (gate)
        {
            if (assignedAt >= lastBackOff)
            {
                delays.Add(startedAt - assignedAt);
            }
        }
    }

    /// <summary>The limit, not the concurrency cap or an empty queue, was what stopped the runner asking for more.</summary>
    public void MarkLimited()
    {
        lock (gate)
        {
            limited = true;
        }
    }

    /// <summary>
    /// Changes the limit if <see cref="AdaptiveStartOptions.AdjustInterval"/> has passed. Jobs still starting count
    /// with their age so far, so pods stuck for a long time back off before any of them start.
    /// </summary>
    /// <returns>Whether the limit changed.</returns>
    public bool Adjust(TimeSpan now, IReadOnlyCollection<TimeSpan> stillStartingSince)
    {
        lock (gate)
        {
            if (now - lastAdjust < options.AdjustInterval)
            {
                return false;
            }

            var completed = new List<TimeSpan>(delays);
            var samples = new List<TimeSpan>(completed);
            samples.AddRange(stillStartingSince.Where(assignedAt => assignedAt >= lastBackOff).Select(assignedAt => now - assignedAt));
            var wasLimited = limited;
            delays.Clear();
            limited = false;
            lastAdjust = now;

            var before = limit;
            var atFloor = limit <= Floor;
            var backedOff = false;
            LastPercentile90 = samples.Count > 0 ? Percentile(samples, 0.9) : null;
            if (LastPercentile90 > CurrentTarget)
            {
                limit = Math.Max(Floor, Math.Floor(limit / 2));
                slowStart = false;
                lastBackOff = now;
                backedOff = true;
            }
            else if (samples.Count > 0 && wasLimited)
            {
                limit = Math.Min(ceiling, slowStart ? limit * 2 : limit + options.Step);
            }

            // Learn the normal start time from intervals that weren't congested, and from the minimum limit,
            // where the runner can't ease off further, so whatever it sees is as fast as the cell starts pods.
            if (completed.Count > 0 && (!backedOff || atFloor))
            {
                baselines.Enqueue((now, Percentile(completed, 0.1)));
            }

            while (baselines.TryPeek(out var oldest) && oldest.At < now - options.BaselineWindow)
            {
                baselines.Dequeue();
            }

            baseline = baselines.Count > 0 ? baselines.Min(b => b.Fast) : null;
            return limit != before;
        }
    }

    /// <summary>Nearest-rank percentile.</summary>
    static TimeSpan Percentile(List<TimeSpan> samples, double p)
    {
        samples.Sort();
        return samples[Math.Max(0, (int)Math.Ceiling(samples.Count * p) - 1)];
    }
}
