using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Kubernetes;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class PodLogFollowerTests
{
    static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task Writes_lines_without_timestamps_and_stops_when_the_container_exits()
    {
        var exit = new TaskCompletionSource<int>();
        var log = new ScriptedLog(_ =>
        {
            exit.TrySetResult(0);
            return Lines("2026-11-09T01:00:00.000000001Z Hello World! 1/2\n", "2026-11-09T01:00:05.5Z Hello World! 2/2\n");
        });
        var output = new RecordingOutput();

        var follower = Follower(log, exit.Task);
        await follower.FollowAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("Hello World! 1/2\nHello World! 2/2\n", output.Text);
        Assert.Equal(0, follower.Reconnects);
        Assert.Single(log.Opens);
        Assert.Equal((null, true), log.Opens[0]);
    }

    [Fact]
    public async Task Reopens_a_reset_stream_and_skips_lines_it_already_wrote()
    {
        var exit = new TaskCompletionSource<int>();
        var log = new ScriptedLog(attempt => attempt switch
        {
            // Cut mid-line, the way an HTTP/2 RST_STREAM(NO_ERROR) cuts it.
            0 => Lines(new HttpProtocolException(0, "The HTTP/2 server reset the stream. HTTP/2 error code 'NO_ERROR' (0x0).", null),
                "2026-11-09T01:00:00Z one\n", "2026-11-09T01:00:05Z two\n", "2026-11-09T01:00:10Z thr"),
            _ => Done(exit, Lines("2026-11-09T01:00:05Z two\n", "2026-11-09T01:00:10Z three\n", "2026-11-09T01:00:15Z four\n")),
        });
        var output = new RecordingOutput();

        var follower = Follower(log, exit.Task);
        await follower.FollowAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("one\ntwo\nthree\nfour\n", output.Text);
        Assert.Equal(1, follower.Reconnects);
        Assert.NotNull(log.Opens[1].SinceSeconds);
        Assert.True(log.Opens[1].SinceSeconds >= PodLogFollower.ClockSkewMargin.TotalSeconds);
    }

    [Fact]
    public async Task Reopens_a_stream_that_ended_while_the_container_was_still_running()
    {
        var exit = new TaskCompletionSource<int>();
        var log = new ScriptedLog(attempt => attempt switch
        {
            0 => Lines("2026-11-09T01:00:00Z one\n"),
            _ => Done(exit, Lines("2026-11-09T01:00:00Z one\n", "2026-11-09T01:00:05Z two\n")),
        });
        var output = new RecordingOutput();

        var follower = Follower(log, exit.Task);
        await follower.FollowAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("one\ntwo\n", output.Text);
        Assert.Equal(1, follower.Reconnects);
    }

    [Fact]
    public async Task Reads_the_rest_without_following_once_the_container_has_exited()
    {
        var exit = new TaskCompletionSource<int>();
        var log = new ScriptedLog(attempt => attempt switch
        {
            0 => Lines(new IOException("cut"), "2026-11-09T01:00:00Z one\n"),
            _ => Lines("2026-11-09T01:00:00Z one\n", "2026-11-09T01:00:05Z last line, no newline"),
        });
        var output = new RecordingOutput();
        exit.SetResult(0);

        var follower = Follower(log, exit.Task);
        await follower.FollowAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("one\nlast line, no newline", output.Text);
        Assert.False(log.Opens[1].Follow);
    }

    [Fact]
    public async Task Keeps_lines_with_the_same_second_but_later_nanoseconds()
    {
        var exit = new TaskCompletionSource<int>();
        var log = new ScriptedLog(attempt => attempt switch
        {
            0 => Lines(new IOException("cut"), "2026-11-09T01:00:00.123456701Z a\n"),
            _ => Done(exit, Lines("2026-11-09T01:00:00.123456701Z a\n", "2026-11-09T01:00:00.123456702Z b\n")),
        });
        var output = new RecordingOutput();

        await Follower(log, exit.Task).FollowAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("a\nb\n", output.Text);
    }

    [Fact]
    public async Task Gives_up_with_the_last_error_after_too_many_reconnects()
    {
        var log = new ScriptedLog(_ => Lines(new IOException("cut")));
        var running = new TaskCompletionSource<int>().Task;

        var follower = Follower(log, running, maxReconnects: 3);
        var error = await Assert.ThrowsAsync<IOException>(() => follower.FollowAsync(new RecordingOutput(), TestContext.Current.CancellationToken));

        Assert.Equal("cut", error.Message);
        Assert.Equal(4, log.Opens.Count);
    }

    static PodLogFollower Follower(ScriptedLog log, Task<int> exit, int maxReconnects = 20) =>
        new(log.Open, exit, TimeProvider.System, NullLogger.Instance, "job-1", maxReconnects, reconnectDelay: Fast, exitConfirmation: TimeSpan.FromMilliseconds(100));

    static Stream Lines(params string[] lines) => new ChunkStream(lines, failWith: null);

    static Stream Lines(Exception failWith, params string[] lines) => new ChunkStream(lines, failWith);

    static Stream Done(TaskCompletionSource<int> exit, Stream stream)
    {
        exit.TrySetResult(0);
        return stream;
    }

    sealed class ScriptedLog(Func<int, Stream> script)
    {
        public List<(int? SinceSeconds, bool Follow)> Opens { get; } = [];

        public Task<Stream> Open(int? sinceSeconds, bool follow, CancellationToken cancellationToken)
        {
            Opens.Add((sinceSeconds, follow));
            return Task.FromResult(script(Opens.Count - 1));
        }
    }

    /// <summary>Returns each chunk from its own read, then ends or throws.</summary>
    sealed class ChunkStream(string[] chunks, Exception? failWith) : Stream
    {
        int next;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (next < chunks.Length)
            {
                var bytes = Encoding.UTF8.GetBytes(chunks[next++]);
                bytes.CopyTo(buffer);
                return ValueTask.FromResult(bytes.Length);
            }

            return failWith is null ? ValueTask.FromResult(0) : ValueTask.FromException<int>(failWith);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    sealed class RecordingOutput : IJobOutput
    {
        readonly MemoryStream bytes = new();

        public string Text => Encoding.UTF8.GetString(bytes.ToArray());

        public void Started(DateTimeOffset startedAt) { }
        public void WaitingForNode() { }
        public void Placed() { }
        public void Write(ReadOnlySpan<byte> data) => bytes.Write(data);
    }
}
