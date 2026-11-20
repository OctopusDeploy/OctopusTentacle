using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Jobs that are in flight but not yet started, and the caps built on that count.</summary>
public sealed class StartingCapacityTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "tspoc-runner-tests", Guid.NewGuid().ToString("N"));
    readonly CapacitySignal signal = new();
    readonly GatedBackend backend = new();
    readonly CancellationToken ct = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        backend.ReleaseAll();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_job_counts_as_starting_until_its_backend_reports_it_started()
    {
        var executor = CreateExecutor(new GatewayOptions());
        executor.Start(Assignment("a"));
        executor.Start(Assignment("b"));
        await Eventually(() => executor.StartingCount == 2);

        backend.Start("a");

        await Eventually(() => executor.StartingCount == 1);
        Assert.Equal(2, executor.InFlightCount);
    }

    [Fact]
    public async Task A_job_that_fails_before_starting_stops_counting_as_starting()
    {
        var executor = CreateExecutor(new GatewayOptions());
        executor.Start(Assignment("a"));
        await Eventually(() => executor.StartingCount == 1);

        backend.FailBeforeStart("a");
        await executor.WhenAllCompleted();

        Assert.Equal(0, executor.StartingCount);
    }

    [Fact]
    public async Task A_job_starting_wakes_the_requester()
    {
        var executor = CreateExecutor(new GatewayOptions());
        executor.Start(Assignment("a"));
        await Eventually(() => executor.StartingCount == 1);
        await signal.WaitAsync(TimeSpan.Zero, ct); // clear any earlier wake-up

        var woke = signal.WaitAsync(TimeSpan.FromSeconds(10), ct);
        backend.Start("a");

        Assert.True(await woke, "The capacity signal didn't fire when the job started.");
    }

    [Fact]
    public async Task Fixed_capacity_pulls_no_more_than_the_starting_cap()
    {
        var options = new GatewayOptions { MaxConcurrentJobs = 10, MaxStartingJobs = 2 };
        var executor = CreateExecutor(options);
        var capacity = new FixedCapacityProvider(executor, Options.Create(options));
        executor.Start(Assignment("a"));
        executor.Start(Assignment("b"));
        await Eventually(() => executor.StartingCount == 2);

        Assert.Equal(0, await capacity.GetAvailableAsync(ct));

        backend.Start("a");
        await Eventually(() => executor.StartingCount == 1);

        // 8 slots are free, but only one more job may be starting.
        Assert.Equal(1, await capacity.GetAvailableAsync(ct));
    }

    [Fact]
    public async Task A_starting_cap_of_zero_means_no_starting_cap()
    {
        var options = new GatewayOptions { MaxConcurrentJobs = 10, MaxStartingJobs = 0 };
        var executor = CreateExecutor(options);
        var capacity = new FixedCapacityProvider(executor, Options.Create(options));
        executor.Start(Assignment("a"));
        executor.Start(Assignment("b"));
        await Eventually(() => executor.StartingCount == 2);

        Assert.Equal(8, await capacity.GetAvailableAsync(ct));
    }

    [Fact]
    public async Task Time_waiting_for_a_node_does_not_count_towards_the_start_delay()
    {
        var executor = CreateExecutor(new GatewayOptions());
        var observed = new TaskCompletionSource<TimeSpan>();
        executor.JobStartObserved += (from, to) => observed.TrySetResult(TimeProvider.System.GetElapsedTime(from, to));
        executor.Start(Assignment("a"));
        await Eventually(() => backend.OutputOf("a") is not null);

        backend.OutputOf("a")!.WaitingForNode();
        Assert.Empty(executor.StartingSince); // waiting for a node isn't a slow start
        Assert.Equal(1, executor.StartingCount); // but it still holds a starting slot
        await Task.Delay(1000, ct);
        backend.OutputOf("a")!.Placed();
        Assert.Single(executor.StartingSince);
        backend.Start("a");

        // Measured from placement, so well under the second spent waiting for a node.
        Assert.True(await observed.Task.WaitAsync(TimeSpan.FromSeconds(10), ct) < TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task Without_waiting_for_a_node_the_start_delay_runs_from_assignment()
    {
        var executor = CreateExecutor(new GatewayOptions());
        var observed = new TaskCompletionSource<TimeSpan>();
        executor.JobStartObserved += (from, to) => observed.TrySetResult(TimeProvider.System.GetElapsedTime(from, to));
        executor.Start(Assignment("a"));
        await Eventually(() => backend.OutputOf("a") is not null);

        await Task.Delay(400, ct);
        backend.OutputOf("a")!.Placed();
        backend.Start("a");

        // At least the 400ms before it got a node (less a little timer slack).
        Assert.True(await observed.Task.WaitAsync(TimeSpan.FromSeconds(10), ct) >= TimeSpan.FromMilliseconds(350));
    }

    [Fact]
    public async Task Adaptive_capacity_pulls_no_more_than_its_current_limit()
    {
        var options = new GatewayOptions { MaxConcurrentJobs = 10, AdaptiveStarts = { Enabled = true, InitialLimit = 2, MinLimit = 1 } };
        var executor = CreateExecutor(options);
        var capacity = new AdaptiveCapacityProvider(executor, Options.Create(options), TimeProvider.System, NullLogger<AdaptiveCapacityProvider>.Instance);
        Assert.Equal(2, await capacity.GetAvailableAsync(ct));

        executor.Start(Assignment("a"));
        executor.Start(Assignment("b"));
        await Eventually(() => executor.StartingCount == 2);

        Assert.Equal(0, await capacity.GetAvailableAsync(ct));
        Assert.Equal(2, capacity.Limit);
    }

    [Fact]
    public async Task Adaptive_capacity_raises_its_limit_after_fast_starts_while_limited()
    {
        var options = new GatewayOptions
        {
            MaxConcurrentJobs = 10,
            AdaptiveStarts = { Enabled = true, InitialLimit = 2, MinLimit = 1, AdjustInterval = TimeSpan.FromMilliseconds(50), TargetStartDelay = TimeSpan.FromSeconds(10) },
        };
        var executor = CreateExecutor(options);
        var capacity = new AdaptiveCapacityProvider(executor, Options.Create(options), TimeProvider.System, NullLogger<AdaptiveCapacityProvider>.Instance);
        executor.Start(Assignment("a"));
        executor.Start(Assignment("b"));
        await Eventually(() => executor.StartingCount == 2);
        Assert.Equal(0, await capacity.GetAvailableAsync(ct)); // the limit is what's holding it back

        backend.Start("a");
        backend.Start("b");
        await Eventually(() => executor.StartingCount == 0);
        await Task.Delay(100, ct);

        Assert.Equal(4, await capacity.GetAvailableAsync(ct)); // doubled: slow start
        Assert.Equal(4, capacity.Limit);
    }

    [Fact]
    public void Adaptive_capacity_uses_the_fixed_starting_cap_as_its_ceiling()
    {
        var options = new GatewayOptions { MaxConcurrentJobs = 100, MaxStartingJobs = 8, AdaptiveStarts = { Enabled = true, InitialLimit = 50 } };
        var capacity = new AdaptiveCapacityProvider(CreateExecutor(options), Options.Create(options), TimeProvider.System, NullLogger<AdaptiveCapacityProvider>.Instance);

        Assert.Equal(8, capacity.Limit);
    }

    GatewayJobExecutor CreateExecutor(GatewayOptions options) => new(
        new JobRunner(
            backend,
            new FileLogStore(Path.GetTempPath()),
            new JobRunnerOptions { LogFlushInterval = TimeSpan.FromMilliseconds(50) }),
        new Outbox(),
        signal,
        TimeProvider.System,
        new StubLifetime(),
        NullLogger<GatewayJobExecutor>.Instance);

    ExecuteJobCommand Assignment(string jobId) => new()
    {
        JobId = jobId,
        RunId = "run-1",
        Command = "unused",
        Storage = new JobStorage { StorageCellId = "local", DirectoryUri = $"{new Uri(root + "/").AbsoluteUri}runs/run-1/{jobId}" },
    };

    internal static async Task Eventually(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    /// <summary>A backend whose jobs start, fail or finish only when the test says so.</summary>
    internal sealed class GatedBackend : IJobBackend
    {
        readonly ConcurrentDictionary<string, Gate> gates = new();
        readonly ConcurrentDictionary<string, IJobOutput> outputs = new();

        public IJobOutput? OutputOf(string jobId) => outputs.GetValueOrDefault(jobId);

        Gate For(string jobId) => gates.GetOrAdd(jobId, _ => new Gate());

        public void Start(string jobId) => For(jobId).Start.TrySetResult(true);

        public void FailBeforeStart(string jobId) => For(jobId).Start.TrySetResult(false);

        public void Finish(string jobId) => For(jobId).Finish.TrySetResult();

        public void ReleaseAll()
        {
            foreach (var gate in gates.Values)
            {
                gate.Start.TrySetResult(true);
                gate.Finish.TrySetResult();
            }
        }

        public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
        {
            var gate = For(job.JobId);
            outputs[job.JobId] = output;
            if (!await gate.Start.Task.WaitAsync(cancellationToken))
            {
                throw new InvalidOperationException("Pod never started.");
            }

            output.Started(DateTimeOffset.UtcNow);
            await gate.Finish.Task.WaitAsync(cancellationToken);
            return 0;
        }

        sealed class Gate
        {
            public TaskCompletionSource<bool> Start { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    sealed class StubLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
