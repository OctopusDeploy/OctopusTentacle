using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Contracts;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// Holds the runner's single outbound stream to the Server cell, reconnecting with backoff, and
/// pulls work over it: the runner asks for jobs when <see cref="ICapacityProvider"/> says it has
/// room. The runner never listens on a port.
/// </summary>
/// <param name="transport">
/// How calls reach the Server: a <see cref="GatewayChannel"/> on the remote hosts, or an <see cref="InProcessCallInvoker"/>
/// bound to the CLI's own RunnerGateway service. The caller owns it.
/// </param>
public sealed class GatewayClient(
    GatewayJobExecutor executor,
    Outbox outbox,
    ICapacityProvider capacity,
    CapacitySignal capacityChanged,
    IOptions<GatewayOptions> options,
    ILogger<GatewayClient> logger,
    CallInvoker transport) : BackgroundService
{
    // A message we read from the outbox but failed to send. Sent first on the next connection.
    RunnerMessage? unsent;

    // Jobs asked for on the current stream that haven't arrived yet. Requests die with their stream,
    // so this goes back to zero on every reconnect.
    int requested;

    // Reconnect timing. Internal so tests can shrink it; not configuration.
    internal TimeSpan InitialBackoff { get; init; } = TimeSpan.FromSeconds(1);

    internal TimeSpan MaxJitter { get; init; } = TimeSpan.FromSeconds(1);

    // How the loop waits between attempts. Internal so tests can record the delays instead of waiting them out.
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    static LogLevel FailureLevel(int consecutiveFailures) => consecutiveFailures <= 1 ? LogLevel.Warning : LogLevel.Debug;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runner = options.Value;
        var client = new RunnerGateway.RunnerGatewayClient(transport);
        var headers = new Metadata();
        if (!string.IsNullOrEmpty(runner.Token))
        {
            headers.Add("authorization", $"Bearer {runner.Token}");
        }

        var backoff = InitialBackoff;
        // Failures in a row since the last Welcome. Only the first is a Warning, so an unreachable Server doesn't flood the log.
        var consecutiveFailures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(client, headers, runner, stoppingToken, onWelcome: () =>
                {
                    backoff = InitialBackoff;
                    consecutiveFailures = 0;
                });
                logger.Log(FailureLevel(++consecutiveFailures), "Server closed the stream");
            }
            catch (Exception ex) when (stoppingToken.IsCancellationRequested
                                       && ex is OperationCanceledException or RpcException { StatusCode: StatusCode.Cancelled })
            {
                // Stopping cancels the call, which gRPC reports as a Cancelled RpcException: not a failure.
                return;
            }
            catch (Exception ex)
            {
                logger.Log(FailureLevel(++consecutiveFailures), "Gateway connection to {Server} failed: {Error}", runner.ServerAddress, ex.Message);
            }

            var delay = backoff + (MaxJitter > TimeSpan.Zero ? Random.Shared.NextDouble() * MaxJitter : TimeSpan.Zero);
            backoff = TimeSpan.FromTicks(Math.Min(runner.MaxReconnectBackoff.Ticks, backoff.Ticks * 2));
            await Delay(delay, stoppingToken);
        }
    }

    async Task RunSessionAsync(RunnerGateway.RunnerGatewayClient client, Metadata headers, GatewayOptions runner, CancellationToken stoppingToken, Action onWelcome)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var call = client.Connect(headers, cancellationToken: session.Token);
        // The outbox pump and the job requester both write to the stream, which allows one writer at a time.
        using var writeLock = new SemaphoreSlim(1, 1);

        var hello = new Hello
        {
            CellId = runner.CellId,
            RunnerId = runner.RunnerId,
        };
        hello.InFlightJobIds.AddRange(executor.InFlightJobIds);
        await call.RequestStream.WriteAsync(new RunnerMessage { Hello = hello }, session.Token);

        if (!await call.ResponseStream.MoveNext(session.Token) || call.ResponseStream.Current.BodyCase != ServerMessage.BodyOneofCase.Welcome)
        {
            throw new InvalidOperationException("Server did not send Welcome.");
        }

        logger.LogInformation("Connected to server {ServerId} as {RunnerId} in cell {CellId} (re-reporting {InFlight} in-flight jobs)",
            call.ResponseStream.Current.Welcome.ServerId, runner.RunnerId, runner.CellId, hello.InFlightJobIds.Count);
        onWelcome();

        Interlocked.Exchange(ref requested, 0);
        var pump = PumpOutboxAsync(call.RequestStream, writeLock, session.Token);
        var requester = AllocateNextJobsAsync(call.RequestStream, writeLock, runner.CapacityCheckInterval, session.Token);
        // If either writer dies, end the session so we reconnect instead of idling on a half-dead stream.
        // Deliberately not disposed: the continuation can run after this method returns, and it owns no timer.
        var writerFailed = new CancellationTokenSource();
        _ = Task.WhenAny(pump, requester).ContinueWith(_ => writerFailed.Cancel(), TaskScheduler.Default);

        // Cancels the read if the Server goes quiet (e.g. a proxy silently dropped the stream) or a writer died.
        using var silence = CancellationTokenSource.CreateLinkedTokenSource(session.Token, writerFailed.Token);
        silence.CancelAfter(runner.ServerSilenceTimeout);
        try
        {
            while (await call.ResponseStream.MoveNext(silence.Token))
            {
                silence.CancelAfter(runner.ServerSilenceTimeout);
                var message = call.ResponseStream.Current;
                switch (message.BodyCase)
                {
                    case ServerMessage.BodyOneofCase.ExecuteJob:
                        // Start before decrementing, so the requester never sees the job as neither requested nor in flight.
                        executor.Start(message.ExecuteJob);
                        Interlocked.Decrement(ref requested);
                        break;
                    case ServerMessage.BodyOneofCase.Heartbeat:
                        outbox.Heartbeat(DateTimeOffset.UtcNow);
                        break;
                }
            }
        }
        catch (Exception ex) when (silence.IsCancellationRequested && !stoppingToken.IsCancellationRequested
                                   && ex is OperationCanceledException or RpcException { StatusCode: StatusCode.Cancelled })
        {
            if (writerFailed.IsCancellationRequested)
            {
                var error = (pump.IsFaulted ? pump : requester).Exception?.GetBaseException();
                throw new IOException($"Sending to the Server failed: {error?.Message ?? "stream closed"}", error);
            }

            throw new TimeoutException($"No message from the Server for {runner.ServerSilenceTimeout.TotalSeconds:0}s; reconnecting.");
        }
        finally
        {
            await session.CancelAsync();
            try
            {
                await Task.WhenAll(pump, requester);
            }
            catch (Exception ex) when (ex is OperationCanceledException or RpcException or IOException or InvalidOperationException)
            {
            }
        }
    }

    /// <summary>
    /// Pulls work: whenever the capacity provider reports more room than is already requested, asks
    /// the Server for the difference. Re-checks after each job finishes and on a fixed interval.
    /// </summary>
    async Task AllocateNextJobsAsync(IClientStreamWriter<RunnerMessage> stream, SemaphoreSlim writeLock, TimeSpan interval, CancellationToken cancellationToken)
    {
        while (true)
        {
            var available = await capacity.GetAvailableAsync(cancellationToken);
            var want = available - Volatile.Read(ref requested);
            if (want > 0)
            {
                Interlocked.Add(ref requested, want);
                await WriteAsync(stream, writeLock, new RunnerMessage { AllocateNextJob = new AllocateNextJobCommand { Count = want } }, cancellationToken);
                logger.LogDebug("Requested {Count} jobs ({Requested} unfilled)", want, Volatile.Read(ref requested));
            }

            await capacityChanged.WaitAsync(interval, cancellationToken);
        }
    }

    async Task PumpOutboxAsync(IClientStreamWriter<RunnerMessage> stream, SemaphoreSlim writeLock, CancellationToken cancellationToken)
    {
        while (true)
        {
            var message = unsent ?? await outbox.Reader.ReadAsync(cancellationToken);
            unsent = message;
            await WriteAsync(stream, writeLock, message, cancellationToken);
            unsent = null;
        }
    }

    static async Task WriteAsync(IClientStreamWriter<RunnerMessage> stream, SemaphoreSlim writeLock, RunnerMessage message, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await stream.WriteAsync(message, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
