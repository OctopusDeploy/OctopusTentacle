using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class AdaptiveStartLimitTests
{
    static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    static readonly TimeSpan Target = TimeSpan.FromSeconds(5);

    static AdaptiveStartOptions Options(int initial = 10, int min = 2, int step = 3) => new()
    {
        Enabled = true,
        InitialLimit = initial,
        MinLimit = min,
        Step = step,
        TargetStartDelay = Target,
        AdjustInterval = Interval,
    };

    static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Starts_at_the_initial_limit_clamped_to_the_ceiling()
    {
        Assert.Equal(10, new AdaptiveStartLimit(Options(initial: 10), ceiling: 100).Limit);
        Assert.Equal(6, new AdaptiveStartLimit(Options(initial: 10), ceiling: 6).Limit);
    }

    [Fact]
    public void Doubles_while_fast_and_limited_until_the_first_back_off()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 1000);

        FastStarts(limit, at: S(1));
        limit.MarkLimited();
        Assert.True(limit.Adjust(S(5), []));
        Assert.Equal(20, limit.Limit);

        FastStarts(limit, at: S(6));
        limit.MarkLimited();
        limit.Adjust(S(10), []);
        Assert.Equal(40, limit.Limit);
    }

    [Fact]
    public void Grows_by_the_step_after_a_back_off()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 40, step: 3), ceiling: 1000);
        limit.RecordStart(assignedAt: S(0), startedAt: S(9));
        limit.Adjust(S(10), []);
        Assert.Equal(20, limit.Limit);

        FastStarts(limit, at: S(11));
        limit.MarkLimited();
        limit.Adjust(S(15), []);

        Assert.Equal(23, limit.Limit);
    }

    [Fact]
    public void Does_not_grow_when_the_limit_was_never_the_constraint()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 1000);
        FastStarts(limit, at: S(1));

        Assert.False(limit.Adjust(S(5), []));
        Assert.Equal(10, limit.Limit);
    }

    [Fact]
    public void Halves_when_the_90th_percentile_start_delay_exceeds_the_target_but_not_below_the_minimum()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10, min: 4), ceiling: 1000);

        // 8 fast starts and 2 slow ones: the 90th percentile is slow.
        FastStarts(limit, at: S(1), count: 8);
        limit.RecordStart(S(0), S(7));
        limit.RecordStart(S(0), S(8));
        limit.MarkLimited();
        Assert.True(limit.Adjust(S(10), []));
        Assert.Equal(5, limit.Limit);

        limit.RecordStart(S(11), S(19));
        limit.Adjust(S(20), []);
        Assert.Equal(4, limit.Limit);
    }

    [Fact]
    public void One_slow_start_in_ten_does_not_back_off()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 1000);
        FastStarts(limit, at: S(1), count: 9);
        limit.RecordStart(S(0), S(30));
        limit.MarkLimited();

        limit.Adjust(S(5), []);

        Assert.Equal(20, limit.Limit);
    }

    [Fact]
    public void Never_exceeds_the_ceiling()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 15);
        FastStarts(limit, at: S(1));
        limit.MarkLimited();

        limit.Adjust(S(5), []);

        Assert.Equal(15, limit.Limit);
    }

    [Fact]
    public void Adjusts_at_most_once_per_interval()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 1000);
        FastStarts(limit, at: S(1));
        limit.MarkLimited();

        Assert.False(limit.Adjust(S(4.9), []));
        Assert.Equal(10, limit.Limit);
        Assert.True(limit.Adjust(S(5), []));
    }

    [Fact]
    public void Jobs_stuck_starting_count_by_their_age_even_before_any_start()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 1000);
        limit.MarkLimited();

        limit.Adjust(S(10), stillStartingSince: [S(1), S(2), S(3)]);

        Assert.Equal(5, limit.Limit);
    }

    [Fact]
    public void Judges_the_current_limit_only_by_jobs_assigned_since_the_last_back_off()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 16), ceiling: 1000);
        limit.Adjust(S(10), stillStartingSince: [S(1), S(2)]);
        Assert.Equal(8, limit.Limit);

        // The same stuck pods, still in kubelet back-off, mustn't halve it again. New jobs start fast.
        FastStarts(limit, at: S(11));
        limit.Adjust(S(15), stillStartingSince: [S(1), S(2), S(14)]);

        Assert.Equal(8, limit.Limit);
    }

    [Fact]
    public void Leaves_the_limit_alone_without_any_samples()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 1000);
        limit.MarkLimited();

        Assert.False(limit.Adjust(S(5), []));
        Assert.Equal(10, limit.Limit);
    }

    [Fact]
    public void Targets_the_minimum_until_it_has_seen_starts()
    {
        Assert.Equal(Target, new AdaptiveStartLimit(Options(), ceiling: 100).Target);
    }

    [Fact]
    public void Target_follows_the_cells_normal_start_time()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 10), ceiling: 1000);
        Starts(limit, at: S(1), delay: S(4));
        limit.MarkLimited();
        limit.Adjust(S(5), []);
        Assert.Equal(S(8), limit.Target);

        // 7s would be slow against the 5s minimum, but it's under twice this cell's normal 4s.
        Starts(limit, at: S(6), delay: S(7));
        limit.MarkLimited();
        limit.Adjust(S(10), []);

        Assert.Equal(40, limit.Limit);
    }

    [Fact]
    public void Never_targets_below_the_minimum()
    {
        var limit = new AdaptiveStartLimit(Options(), ceiling: 1000);
        Starts(limit, at: S(1), delay: S(1));
        limit.Adjust(S(5), []);

        Assert.Equal(Target, limit.Target);
    }

    [Fact]
    public void Congested_intervals_do_not_raise_the_target()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 40), ceiling: 1000);
        Starts(limit, at: S(1), delay: S(4));
        limit.Adjust(S(5), []);

        Starts(limit, at: S(6), delay: S(20));
        limit.Adjust(S(10), []);

        Assert.Equal(20, limit.Limit);
        Assert.Equal(S(8), limit.Target);
    }

    [Fact]
    public void Learns_the_normal_start_time_at_the_minimum_limit_even_when_slow()
    {
        var limit = new AdaptiveStartLimit(Options(initial: 4, min: 4), ceiling: 1000);

        // At the minimum the runner can't ease off any further, so this is as fast as the cell starts pods.
        Starts(limit, at: S(1), delay: S(6));
        limit.Adjust(S(10), []);
        Assert.Equal(S(12), limit.Target);

        Starts(limit, at: S(11), delay: S(10));
        limit.MarkLimited();
        limit.Adjust(S(20), []);
        Assert.Equal(7, limit.Limit); // the back-off ended slow start, so it grows by the step
    }

    [Fact]
    public void Forgets_fast_starts_older_than_the_baseline_window()
    {
        var options = Options(initial: 4, min: 4);
        options.BaselineWindow = TimeSpan.FromSeconds(30);
        var limit = new AdaptiveStartLimit(options, ceiling: 4);
        Starts(limit, at: S(1), delay: S(4));
        limit.Adjust(S(5), []);

        for (var t = 10; t <= 40; t += 5)
        {
            Starts(limit, at: S(t - 4), delay: S(6));
            limit.Adjust(S(t), []);
            Assert.Equal(t < 40 ? S(8) : S(12), limit.Target);
        }
    }

    static void Starts(AdaptiveStartLimit limit, TimeSpan at, TimeSpan delay, int count = 5)
    {
        for (var i = 0; i < count; i++)
        {
            limit.RecordStart(assignedAt: at, startedAt: at + delay);
        }
    }

    static void FastStarts(AdaptiveStartLimit limit, TimeSpan at, int count = 5)
    {
        for (var i = 0; i < count; i++)
        {
            limit.RecordStart(assignedAt: at, startedAt: at + TimeSpan.FromSeconds(1));
        }
    }
}
