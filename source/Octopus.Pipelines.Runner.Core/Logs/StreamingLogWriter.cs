using Microsoft.Extensions.Logging;

namespace Octopus.Pipelines.Runner.Core.Logs;

/// <summary>
/// Buffers a job's output and appends it to the storage cell on a timer or once enough bytes
/// pile up. This trades a second or so of tail latency for far fewer storage requests: at 10k
/// concurrent jobs, per-line writes would be hundreds of thousands of requests per second.
/// </summary>
public sealed class StreamingLogWriter : IAsyncDisposable
{
    readonly ILogAppender appender;
    readonly TimeSpan flushInterval;
    readonly int flushBytes;
    readonly long maxPendingBytes;
    readonly ILogger logger;
    readonly Lock gate = new();
    readonly SemaphoreSlim wake = new(0);
    readonly CancellationTokenSource stop = new();
    readonly CancellationTokenSource faulted = new();
    readonly Task loop;
    MemoryStream pending = new();
    int wakeRequested;
    bool completed;
    long inFlightBytes;
    LogStorageException? fault;

    public StreamingLogWriter(ILogAppender appender, TimeSpan flushInterval, int flushBytes, long maxPendingBytes, ILogger logger)
    {
        this.appender = appender;
        this.flushInterval = flushInterval;
        this.flushBytes = flushBytes;
        this.maxPendingBytes = maxPendingBytes;
        this.logger = logger;
        loop = Task.Run(LoopAsync);
    }

    /// <summary>Bytes durably written to the storage cell so far.</summary>
    public long Length => appender.Length;

    /// <summary>
    /// Cancelled when the log can no longer be written: a permanent storage failure, or more unflushed bytes
    /// than the cap. Output written after that is dropped, and <see cref="CompleteAsync"/> throws.
    /// </summary>
    public CancellationToken Faulted => faulted.Token;

    public void Write(ReadOnlySpan<byte> data)
    {
        bool full;
        LogStorageException? overflow = null;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(completed, this);
            if (fault is not null)
            {
                return;
            }

            if (pending.Length + inFlightBytes + data.Length > maxPendingBytes)
            {
                overflow = new LogStorageException($"More than {maxPendingBytes} bytes of log output are waiting to be stored.", permanent: true);
            }

            full = false;
            if (overflow is null)
            {
                pending.Write(data);
                full = pending.Length >= flushBytes;
            }
        }

        if (overflow is not null)
        {
            // Outside the gate: cancelling runs the token's callbacks inline.
            Fault(overflow);
            return;
        }

        if (full && Interlocked.Exchange(ref wakeRequested, 1) == 0)
        {
            wake.Release();
        }
    }

    /// <summary>Flushes everything that's buffered. The log is complete (durable) when this returns.</summary>
    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (completed)
            {
                return;
            }

            completed = true;
        }

        await stop.CancelAsync();
        await loop;
        ThrowIfFaulted();
        await FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await CompleteAsync(CancellationToken.None);
        }
        finally
        {
            await appender.DisposeAsync();
            stop.Dispose();
            wake.Dispose();
            faulted.Dispose();
        }
    }

    void Fault(LogStorageException ex)
    {
        // The first fault wins.
        if (Interlocked.CompareExchange(ref fault, ex, null) is null)
        {
            faulted.Cancel();
        }
    }

    void ThrowIfFaulted()
    {
        if (Volatile.Read(ref fault) is { } ex)
        {
            throw new LogStorageException(ex.Message, ex.Permanent, ex);
        }
    }

    async Task LoopAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await wake.WaitAsync(flushInterval, stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Interlocked.Exchange(ref wakeRequested, 0);
            try
            {
                await FlushAsync(stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (LogStorageException ex) when (ex.Permanent)
            {
                logger.LogWarning(ex, "Log storage failed permanently");
                Fault(ex);
                return;
            }
            catch (Exception ex)
            {
                // Data stays buffered and is retried on the next tick.
                logger.LogWarning(ex, "Log flush failed; will retry");
            }
        }
    }

    async Task FlushAsync(CancellationToken cancellationToken)
    {
        byte[] data;
        lock (gate)
        {
            if (pending.Length == 0)
            {
                return;
            }

            data = pending.ToArray();
            pending = new MemoryStream();
            inFlightBytes = data.Length;
        }

        try
        {
            await appender.AppendAsync(data, cancellationToken);
            Volatile.Write(ref inFlightBytes, 0);
        }
        catch
        {
            // Put the unwritten bytes back in front of anything written since.
            lock (gate)
            {
                inFlightBytes = 0;
                var restored = new MemoryStream();
                restored.Write(data);
                pending.Position = 0;
                pending.CopyTo(restored);
                pending = restored;
            }

            throw;
        }
    }
}
