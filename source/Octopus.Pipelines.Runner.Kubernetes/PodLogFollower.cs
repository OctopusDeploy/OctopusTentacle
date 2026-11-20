using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Core;
using System.Globalization;
using System.Text;
using k8s.Autorest;

namespace Octopus.Pipelines.Runner.Kubernetes;

/// <summary>Opens a container log stream: (sinceSeconds, follow, cancellation). Lines must be timestamped.</summary>
public delegate Task<Stream> OpenPodLog(int? sinceSeconds, bool follow, CancellationToken cancellationToken);

/// <summary>
/// Follows a job container's log until the container exits, and survives the stream being cut. Long follows
/// through the API server do get cut: under load we saw the HTTP/2 stream reset (NO_ERROR) and streams end
/// cleanly while the container was still running. Neither says anything about the job, whose result comes
/// from the pod watch (<paramref name="terminated"/>), so a cut stream is reopened instead of failing the job.
/// </summary>
/// <remarks>
/// Lines are requested with timestamps. A reopened stream starts a little before the last line we kept
/// (<c>sinceSeconds</c> plus <see cref="ClockSkewMargin"/>, since the node's clock isn't ours), and lines at or
/// before that timestamp are dropped, so nothing is repeated. The timestamps are stripped before output.
/// </remarks>
public sealed class PodLogFollower(
    OpenPodLog open,
    Task<int> terminated,
    TimeProvider clock,
    ILogger logger,
    string podName,
    int maxReconnects = 20,
    TimeSpan? reconnectDelay = null,
    TimeSpan? exitConfirmation = null)
{
    public static readonly TimeSpan ClockSkewMargin = TimeSpan.FromSeconds(10);

    readonly TimeSpan reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(1);

    // The kubelet ends a follow when the container exits; the pod watch usually confirms that within a second or two.
    readonly TimeSpan exitConfirmation = exitConfirmation ?? TimeSpan.FromSeconds(5);

    /// <summary>How many times the log stream was reopened.</summary>
    public int Reconnects { get; private set; }

    public async Task FollowAsync(IJobOutput output, CancellationToken cancellationToken)
    {
        var lines = new TimestampedLines(output);
        var buffer = new byte[16 * 1024];
        while (true)
        {
            // Once the container has exited, one complete (non-follow) read has the whole log.
            var exited = terminated.IsCompleted;
            Exception? failure = null;
            try
            {
                await using var stream = await open(SinceSeconds(lines.Last), follow: !exited, cancellationToken);
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    lines.Feed(buffer.AsSpan(0, read));
                }
            }
            catch (Exception ex) when (IsStreamFailure(ex, cancellationToken))
            {
                failure = ex;
            }

            if (failure is null && (exited || await ExitConfirmedAsync(cancellationToken)))
            {
                lines.Flush();
                return;
            }

            if (++Reconnects > maxReconnects)
            {
                throw failure ?? new IOException($"The log stream for {podName} kept ending while the container was still running.");
            }

            // Its line will come again, whole, on the reopened stream.
            lines.DiscardPartialLine();
            if (failure is not null)
            {
                logger.LogWarning("Log stream for {Pod} failed ({Error}); reopening ({Reconnect}/{Max})",
                    podName, $"{failure.GetType().Name}: {failure.Message}", Reconnects, maxReconnects);
            }
            else
            {
                logger.LogWarning("Log stream for {Pod} ended while the container was still running; reopening ({Reconnect}/{Max})",
                    podName, Reconnects, maxReconnects);
            }

            await Task.Delay(reconnectDelay, clock, cancellationToken);
        }
    }

    async Task<bool> ExitConfirmedAsync(CancellationToken cancellationToken)
    {
        // A failed pod (e.g. deleted) counts too: the caller's wait on the tracker reports why.
        await Task.WhenAny(terminated, Task.Delay(exitConfirmation, clock, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        return terminated.IsCompleted;
    }

    int? SinceSeconds(DateTimeOffset? last) => last is { } at
        ? Math.Max(1, (int)Math.Ceiling((clock.GetUtcNow() - at + ClockSkewMargin).TotalSeconds))
        : null;

    static bool IsStreamFailure(Exception ex, CancellationToken cancellationToken) => ex switch
    {
        // HttpProtocolException ("The HTTP/2 server reset the stream") is an IOException.
        IOException or HttpRequestException or HttpOperationException => true,
        // HttpClient's own timeout, not ours.
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };
}

/// <summary>
/// Splits a timestamped container log (<c>2026-11-09T01:02:03.123456789Z message</c>) into lines, drops
/// lines at or before the last one written, and writes the rest without their timestamps.
/// </summary>
sealed class TimestampedLines(IJobOutput output)
{
    readonly MemoryStream partial = new();
    LogTimestamp? last;

    public DateTimeOffset? Last => last?.At;

    public void Feed(ReadOnlySpan<byte> data)
    {
        while (data.Length > 0)
        {
            var newline = data.IndexOf((byte)'\n');
            if (newline < 0)
            {
                partial.Write(data);
                return;
            }

            if (partial.Length == 0)
            {
                Line(data[..(newline + 1)]);
            }
            else
            {
                partial.Write(data[..(newline + 1)]);
                Line(partial.GetBuffer().AsSpan(0, (int)partial.Length));
                partial.SetLength(0);
            }

            data = data[(newline + 1)..];
        }
    }

    /// <summary>Writes a last line that had no newline (the stream ended for good).</summary>
    public void Flush()
    {
        if (partial.Length > 0)
        {
            Line(partial.GetBuffer().AsSpan(0, (int)partial.Length));
            partial.SetLength(0);
        }
    }

    public void DiscardPartialLine() => partial.SetLength(0);

    void Line(ReadOnlySpan<byte> line)
    {
        var space = line.IndexOf((byte)' ');
        if (space <= 0 || !LogTimestamp.TryParse(line[..space], out var at))
        {
            // Not timestamped, so it can't be deduplicated: keep it as it is.
            output.Write(line);
            return;
        }

        if (last is { } previous && at.CompareTo(previous) <= 0)
        {
            return;
        }

        last = at;
        output.Write(line[(space + 1)..]);
    }
}

/// <summary>An RFC 3339 timestamp with up to nanosecond precision (DateTimeOffset stops at 100ns).</summary>
readonly record struct LogTimestamp(DateTimeOffset At, int ExtraNanoseconds) : IComparable<LogTimestamp>
{
    public int CompareTo(LogTimestamp other)
    {
        var byTicks = At.UtcTicks.CompareTo(other.At.UtcTicks);
        return byTicks != 0 ? byTicks : ExtraNanoseconds.CompareTo(other.ExtraNanoseconds);
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8, out LogTimestamp timestamp)
    {
        timestamp = default;
        if (utf8.Length > 64)
        {
            return false;
        }

        var text = Encoding.ASCII.GetString(utf8);
        var extra = 0;
        var dot = text.IndexOf('.');
        if (dot >= 0)
        {
            var end = dot + 1;
            while (end < text.Length && char.IsAsciiDigit(text[end]))
            {
                end++;
            }

            var digits = text[(dot + 1)..end];
            if (digits.Length > 7)
            {
                // Digits 8 and 9 are tens and units of nanoseconds; DateTimeOffset keeps the first 7.
                extra = int.Parse(digits[7..Math.Min(9, digits.Length)].PadRight(2, '0'), CultureInfo.InvariantCulture);
                text = text[..(dot + 8)] + text[end..];
            }
        }

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at))
        {
            return false;
        }

        timestamp = new LogTimestamp(at, extra);
        return true;
    }
}
