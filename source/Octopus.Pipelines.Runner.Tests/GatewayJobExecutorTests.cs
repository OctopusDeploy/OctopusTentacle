using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class GatewayJobExecutorTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "tspoc-runner-tests", Guid.NewGuid().ToString("N"));
    readonly Outbox outbox = new();
    readonly CapacitySignal signal = new();

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Runs_the_command_streams_its_log_and_reports_start_then_completion()
    {
        var executor = CreateExecutor();
        executor.Start(Assignment("job-1", "echo 'Hello World!'"));

        var started = await NextAsync();
        var completed = await NextAsync();

        Assert.Equal("job-1", started.JobStarted.JobId);
        Assert.Equal("job-1", completed.MarkJobAsFinished.JobId);
        Assert.Equal(JobOutcome.Succeeded, completed.MarkJobAsFinished.Outcome);
        Assert.Equal(13, completed.MarkJobAsFinished.LogBytes);
        Assert.Equal("Hello World!\n", await File.ReadAllTextAsync(Path.Combine(root, "runs/run-1/job-1/job.log"), TestContext.Current.CancellationToken));
        Assert.Empty(executor.InFlightJobIds);
    }

    [Fact]
    public async Task Reports_a_failed_job_with_its_exit_code()
    {
        var executor = CreateExecutor();
        executor.Start(Assignment("job-2", "echo boom >&2; exit 3"));

        await NextAsync();
        var completed = (await NextAsync()).MarkJobAsFinished;

        Assert.Equal(JobOutcome.Failed, completed.Outcome);
        Assert.Equal(3, completed.ExitCode);
        Assert.Equal("boom\n", await File.ReadAllTextAsync(Path.Combine(root, "runs/run-1/job-2/job.log"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reports_jobs_as_in_flight_until_they_finish()
    {
        var executor = CreateExecutor();
        executor.Start(Assignment("job-3", "sleep 1"));

        await NextAsync();
        Assert.Contains("job-3", executor.InFlightJobIds);

        await NextAsync();
        await executor.WhenAllCompleted();
        Assert.Empty(executor.InFlightJobIds);
    }

    [Fact]
    public async Task Finishing_a_job_frees_fixed_capacity_and_wakes_the_requester()
    {
        var options = new GatewayOptions { MaxConcurrentJobs = 2 };
        var executor = CreateExecutor();
        var capacity = new FixedCapacityProvider(executor, Options.Create(options));
        var ct = TestContext.Current.CancellationToken;

        executor.Start(Assignment("job-4", "sleep 1"));
        Assert.Equal(1, await capacity.GetAvailableAsync(ct));

        // Wakes well before the 10s timeout because the job finishes after about a second.
        var woke = Task.Run(() => signal.WaitAsync(TimeSpan.FromSeconds(10), ct), ct);
        await executor.WhenAllCompleted();
        Assert.True(await woke, "The capacity signal didn't fire when the job finished.");
        Assert.Equal(2, await capacity.GetAvailableAsync(ct));
    }

    [Fact]
    public async Task Fails_a_sandboxed_job_rather_than_running_it_without_a_sandbox()
    {
        var executor = CreateExecutor();
        var assignment = Assignment("job-5", "touch ran");
        assignment.Isolation = Octopus.Pipelines.Runner.Contracts.JobIsolation.Sandboxed;
        executor.Start(assignment);

        var completed = (await NextAsync()).MarkJobAsFinished;

        Assert.Equal(JobOutcome.Failed, completed.Outcome);
        Assert.Contains("Sandboxed jobs need the Kubernetes backend", completed.Error);
        Assert.False(File.Exists("ran"));
    }

    [Fact]
    public async Task Maps_UNSPECIFIED_isolation_to_standard()
    {
        var executor = CreateExecutor();
        var assignment = Assignment("job-6", "true");
        assignment.Isolation = Octopus.Pipelines.Runner.Contracts.JobIsolation.Unspecified;
        executor.Start(assignment);

        await NextAsync();
        var completed = (await NextAsync()).MarkJobAsFinished;

        Assert.Equal(JobOutcome.Succeeded, completed.Outcome);
    }

    [Fact]
    public async Task Hands_the_whole_job_directory_URI_to_the_log_store()
    {
        var store = new RecordingStore(root);
        var executor = CreateExecutor(store);
        var assignment = Assignment("j1", "true");
        assignment.Storage.DirectoryUri = $"{new Uri(root + "/").AbsoluteUri}c-x/runs/r1/j1?sv=1&sig=abc";
        executor.Start(assignment);

        await NextAsync(); // started

        Assert.Equal(new Uri(assignment.Storage.DirectoryUri), store.Directory);
        Assert.Equal(JobFiles.Log, store.FileName);
    }

    sealed class RecordingStore(string root) : ILogStore
    {
        readonly FileLogStore files = new(root);

        public Uri? Directory { get; private set; }

        public string? FileName { get; private set; }

        public Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
        {
            Directory = jobDirectory;
            FileName = fileName;
            return files.CreateAsync(jobDirectory, fileName, ct);
        }
    }

    GatewayJobExecutor CreateExecutor(ILogStore? store = null) => new(
        new JobRunner(
            new ProcessJobBackend(TimeProvider.System),
            store ?? new FileLogStore(root),
            new JobRunnerOptions { LogFlushInterval = TimeSpan.FromMilliseconds(50) }),
        outbox,
        signal,
        TimeProvider.System,
        new StubLifetime(),
        NullLogger<GatewayJobExecutor>.Instance);

    ExecuteJobCommand Assignment(string jobId, string command) => new()
    {
        JobId = jobId,
        RunId = "run-1",
        Command = command,
        Storage = new JobStorage { StorageCellId = "local", DirectoryUri = $"{new Uri(root + "/").AbsoluteUri}runs/run-1/{jobId}" },
    };

    async Task<RunnerMessage> NextAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await outbox.Reader.ReadAsync(timeout.Token);
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
