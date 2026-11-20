using System.Text;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class JobRunnerTests
{
    static readonly Uri Directory = new("https://account.blob.core.windows.net/c-customer/runs/run/job?sas");
    static readonly JobSpec Job = new("job", "run", "echo hi");

    static JobRunner CreateRunner(IJobBackend backend, ILogStore store) =>
        new(backend, store, new JobRunnerOptions { LogFlushInterval = TimeSpan.FromMinutes(1) });

    [Fact]
    public async Task Runs_the_job_and_returns_once_the_log_is_durable()
    {
        var store = new MemoryLogStore();
        var backend = new FakeBackend { Output = "a\nb\n", StartedAt = DateTimeOffset.UnixEpoch };

        var result = await CreateRunner(backend, store).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(4, result.LogBytes);
        Assert.Null(result.Error);
        Assert.Equal("a\nb\n", store.Text(JobFiles.Log));
        Assert.NotNull(result.StartedAt);
    }

    [Fact]
    public async Task Reports_a_non_zero_exit_as_failed()
    {
        var backend = new FakeBackend { ExitCode = 3 };

        var result = await CreateRunner(backend, new MemoryLogStore()).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.ExitCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Turns_a_backend_exception_into_a_failed_result()
    {
        var backend = new FakeBackend { Throw = new NotSupportedException("no sandbox") };

        var result = await CreateRunner(backend, new MemoryLogStore()).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(-1, result.ExitCode);
        Assert.Equal("NotSupportedException: no sandbox", result.Error);
    }

    [Fact]
    public async Task Fails_the_job_when_its_log_cant_be_created()
    {
        var store = new MemoryLogStore { FailCreate = true };
        var backend = new FakeBackend();

        var result = await CreateRunner(backend, store).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Log storage: ", result.Error);
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public async Task Reports_shutdown_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var running = new TaskCompletionSource();
        var backend = new FakeBackend
        {
            Run = async (_, token) =>
            {
                running.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            },
        };

        var task = CreateRunner(backend, new MemoryLogStore()).RunAsync(Job, Directory, null, cts.Token);
        await running.Task;
        await cts.CancelAsync();
        var result = await task;

        Assert.False(result.Succeeded);
        Assert.Equal("Runner shutting down.", result.Error);
    }

    [Fact]
    public async Task Stops_a_running_job_when_its_log_storage_fails_permanently()
    {
        var store = new MemoryLogStore { AppendFails = () => new LogStorageException("expired", permanent: true) };
        var sawCancellation = new TaskCompletionSource();
        var backend = new FakeBackend
        {
            Run = async (output, token) =>
            {
                output.Write("data\n"u8);
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    sawCancellation.SetResult();
                    throw;
                }

                return 0;
            },
        };
        var runner = new JobRunner(backend, store, new JobRunnerOptions { LogFlushInterval = TimeSpan.FromMilliseconds(20) });

        var result = await runner.RunAsync(Job, Directory, null, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Log storage: ", result.Error);
        await sawCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stops_a_running_process_job_when_its_log_storage_fails_permanently()
    {
        var store = new MemoryLogStore { AppendFails = () => new LogStorageException("expired", permanent: true) };
        var observer = new PidObserver();
        var runner = new JobRunner(new ProcessJobBackend(TimeProvider.System), store, new JobRunnerOptions { LogFlushInterval = TimeSpan.FromMilliseconds(20) });

        var result = await runner.RunAsync(Job with { Command = ProcessJobBackendTests.SleepingChild }, Directory, observer, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Log storage: ", result.Error);
        var pid = await observer.Pid.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(await ProcessProbe.ExitsWithinAsync(pid, TimeSpan.FromSeconds(5)), $"Process {pid} is still running after its log failed.");
    }

    [Fact]
    public async Task Reports_a_stray_cancellation_as_a_backend_error()
    {
        // Neither the caller nor the log cancelled this: it's the backend's own failure, e.g. an HTTP timeout.
        var backend = new FakeBackend { Throw = new TaskCanceledException("timed out") };

        var result = await CreateRunner(backend, new MemoryLogStore()).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("TaskCanceledException: timed out", result.Error);
    }

    [Fact]
    public async Task Keeps_the_backend_error_when_the_log_also_fails_to_complete()
    {
        var store = new MemoryLogStore { AppendFails = () => new IOException("denied") };
        var backend = new FakeBackend { Output = "x\n", Throw = new InvalidOperationException("boom") };

        var result = await CreateRunner(backend, store).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("InvalidOperationException: boom", result.Error);
    }

    [Fact]
    public async Task Fails_a_successful_run_whose_log_fails_to_complete()
    {
        var store = new MemoryLogStore { AppendFails = () => new IOException("denied") };
        var backend = new FakeBackend { Output = "x\n" };

        var result = await CreateRunner(backend, store).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Log storage: ", result.Error);
    }

    [Fact]
    public async Task Creates_an_empty_log_for_a_job_with_no_output()
    {
        var store = new MemoryLogStore();

        var result = await CreateRunner(new FakeBackend(), store).RunAsync(Job, Directory, null, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.LogBytes);
        Assert.Equal("", store.Text(JobFiles.Log));
    }

    [Fact]
    public async Task Tells_the_observer_about_start_and_output()
    {
        var store = new MemoryLogStore();
        var backend = new FakeBackend { Output = "a\nb\n", StartedAt = DateTimeOffset.UnixEpoch, StartTwice = true };
        var observer = new RecordingObserver();

        await CreateRunner(backend, store).RunAsync(Job, Directory, observer, TestContext.Current.CancellationToken);

        Assert.Equal([DateTimeOffset.UnixEpoch], observer.Starts);
        Assert.Equal("a\nb\n", observer.Text);
        Assert.Equal(store.Text(JobFiles.Log), observer.Text);
    }

    sealed class FakeBackend : IJobBackend
    {
        public string Output { get; init; } = "";
        public int ExitCode { get; init; }
        public Exception? Throw { get; init; }
        public DateTimeOffset? StartedAt { get; init; }
        public bool StartTwice { get; init; }
        public Func<IJobOutput, CancellationToken, Task<int>>? Run { get; init; }
        public int Calls { get; private set; }

        public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
        {
            Calls++;
            if (Run is not null)
            {
                return await Run(output, cancellationToken);
            }

            if (StartedAt is { } startedAt)
            {
                output.Started(startedAt);
                if (StartTwice)
                {
                    output.Started(startedAt.AddSeconds(1));
                }
            }

            if (Output.Length > 0)
            {
                output.Write(Encoding.UTF8.GetBytes(Output));
            }

            if (Throw is not null)
            {
                throw Throw;
            }

            await Task.CompletedTask;
            return ExitCode;
        }
    }

    sealed class RecordingObserver : IJobObserver
    {
        readonly MemoryStream output = new();
        public List<DateTimeOffset> Starts { get; } = [];
        public string Text => Encoding.UTF8.GetString(output.ToArray());

        public void Started(DateTimeOffset startedAt) => Starts.Add(startedAt);

        public void Output(ReadOnlySpan<byte> data) => output.Write(data);
    }

    sealed class PidObserver : IJobObserver
    {
        readonly StringBuilder text = new();
        readonly TaskCompletionSource<int> pid = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> Pid => pid.Task;

        public void Output(ReadOnlySpan<byte> data)
        {
            lock (text)
            {
                text.Append(Encoding.UTF8.GetString(data));
                if (ProcessProbe.TryParsePid(text.ToString(), out var value))
                {
                    pid.TrySetResult(value);
                }
            }
        }
    }

    sealed class MemoryLogStore : ILogStore
    {
        readonly Dictionary<string, MemoryStream> files = [];
        public bool FailCreate { get; init; }
        public Func<Exception>? AppendFails { get; init; }

        public string Text(string fileName) => Encoding.UTF8.GetString(files[fileName].ToArray());

        public Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
        {
            if (FailCreate)
            {
                throw new IOException("denied");
            }

            var stream = files[fileName] = new MemoryStream();
            return Task.FromResult<ILogAppender>(new Appender(stream, AppendFails));
        }

        sealed class Appender(MemoryStream stream, Func<Exception>? fails) : ILogAppender
        {
            public long Length => stream.Length;

            public Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
            {
                if (fails is not null)
                {
                    throw fails();
                }

                stream.Write(data.Span);
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
