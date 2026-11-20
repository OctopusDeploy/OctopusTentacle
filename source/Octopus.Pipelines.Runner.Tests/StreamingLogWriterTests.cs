using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Octopus.Pipelines.Runner.Core.Logs;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class StreamingLogWriterTests
{
    const long MaxPending = 64L * 1024 * 1024;

    [Fact]
    public async Task Batches_small_writes_into_one_append_on_completion()
    {
        var appender = new RecordingAppender();
        await using var writer = new StreamingLogWriter(appender, TimeSpan.FromMinutes(1), flushBytes: 1024, MaxPending, NullLogger.Instance);

        writer.Write("hello "u8);
        writer.Write("world\n"u8);
        await writer.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Single(appender.Appends);
        Assert.Equal("hello world\n", appender.Text);
        Assert.Equal(12, writer.Length);
    }

    [Fact]
    public async Task Flushes_on_the_interval_while_the_job_is_still_running()
    {
        var appender = new RecordingAppender();
        await using var writer = new StreamingLogWriter(appender, TimeSpan.FromMilliseconds(50), flushBytes: 1024, MaxPending, NullLogger.Instance);

        writer.Write("tick\n"u8);
        await WaitUntil(() => appender.Text == "tick\n");
    }

    [Fact]
    public async Task Flushes_early_once_the_buffer_threshold_is_reached()
    {
        var appender = new RecordingAppender();
        await using var writer = new StreamingLogWriter(appender, TimeSpan.FromMinutes(1), flushBytes: 8, MaxPending, NullLogger.Instance);

        writer.Write("0123456789"u8);
        await WaitUntil(() => appender.Text == "0123456789");
    }

    [Fact]
    public async Task Keeps_order_when_a_flush_fails_and_is_retried()
    {
        var appender = new RecordingAppender { FailNext = 1 };
        await using var writer = new StreamingLogWriter(appender, TimeSpan.FromMilliseconds(30), flushBytes: 1024, MaxPending, NullLogger.Instance);

        writer.Write("first\n"u8);
        await WaitUntil(() => appender.Failures == 1);
        writer.Write("second\n"u8);
        await writer.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal("first\nsecond\n", appender.Text);
    }

    [Fact]
    public async Task Keeps_retrying_transient_failures()
    {
        var appender = new RecordingAppender { FailNext = 2 };
        await using var writer = new StreamingLogWriter(appender, TimeSpan.FromMilliseconds(20), flushBytes: 1024, MaxPending, NullLogger.Instance);

        writer.Write("one\n"u8);
        await WaitUntil(() => appender.Failures == 2);
        writer.Write("two\n"u8);
        await writer.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.False(writer.Faulted.IsCancellationRequested);
        Assert.Equal("one\ntwo\n", appender.Text);
    }

    [Fact]
    public async Task Faults_on_a_permanent_append_failure()
    {
        var appender = new RecordingAppender { Fail = () => new LogStorageException("expired", permanent: true) };
        await using var writer = new StreamingLogWriter(appender, TimeSpan.FromMilliseconds(20), flushBytes: 1024, MaxPending, NullLogger.Instance);

        writer.Write("data\n"u8);
        await WaitForFault(writer);

        Assert.True(writer.Faulted.IsCancellationRequested);
        var ex = await Assert.ThrowsAsync<LogStorageException>(() => writer.CompleteAsync(TestContext.Current.CancellationToken));
        Assert.True(ex.Permanent);
    }

    [Fact]
    public async Task Faults_when_unflushed_bytes_pass_the_cap()
    {
        var appender = new RecordingAppender { FailNext = int.MaxValue };
        await using var writer = new StreamingLogWriter(appender, TimeSpan.FromMilliseconds(20), flushBytes: 1024, maxPendingBytes: 1024, NullLogger.Instance);

        writer.Write(new byte[2048]);
        await WaitForFault(writer);

        Assert.True(writer.Faulted.IsCancellationRequested);
        await Assert.ThrowsAsync<LogStorageException>(() => writer.CompleteAsync(TestContext.Current.CancellationToken));
    }

    static async Task WaitForFault(StreamingLogWriter writer)
    {
        var faulted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = writer.Faulted.Register(() => faulted.TrySetResult());
        await faulted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for condition.");
            await Task.Delay(10);
        }
    }

    sealed class RecordingAppender : ILogAppender
    {
        readonly Lock gate = new();
        readonly StringBuilder text = new();

        public List<int> Appends { get; } = [];
        public int FailNext { get; set; }
        public Func<Exception>? Fail { get; init; }
        public int Failures { get; private set; }
        public long Length { get; private set; }

        public string Text
        {
            get
            {
                lock (gate)
                {
                    return text.ToString();
                }
            }
        }

        public Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (Fail is not null)
                {
                    Failures++;
                    throw Fail();
                }

                if (FailNext > 0)
                {
                    FailNext--;
                    Failures++;
                    throw new IOException("storage unavailable");
                }

                Appends.Add(data.Length);
                text.Append(Encoding.UTF8.GetString(data.Span));
                Length += data.Length;
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
