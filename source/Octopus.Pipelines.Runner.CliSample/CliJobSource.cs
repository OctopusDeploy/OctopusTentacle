using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Octopus.Pipelines.Runner.Contracts;

namespace Octopus.Pipelines.Runner.CliSample;

/// <summary>
/// The CLI's side of the RunnerGateway contract: it plays the Server for one job. The runner connects to it in
/// process (see <c>InProcessCallInvoker</c>), so the job goes through the same gateway code as on the remote hosts.
/// </summary>
sealed class CliJobSource(ExecuteJobCommand job, TimeSpan heartbeatInterval) : RunnerGateway.RunnerGatewayBase
{
    int handedOut;

    /// <summary>The runner reported the job's container as started.</summary>
    public TaskCompletionSource<DateTimeOffset> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The runner reported the job as finished. Its log is durable by then.</summary>
    public TaskCompletionSource<MarkJobAsFinishedCommand> Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async Task Connect(IAsyncStreamReader<RunnerMessage> requestStream, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
    {
        // The heartbeat and the replies below both write to the stream, which allows one write at a time.
        using var writeLock = new SemaphoreSlim(1, 1);
        async Task WriteAsync(ServerMessage message)
        {
            await writeLock.WaitAsync(context.CancellationToken);
            try
            {
                await responseStream.WriteAsync(message);
            }
            finally
            {
                writeLock.Release();
            }
        }

        // Like the Server, keep the stream busy: the runner reconnects if it hears nothing for a while.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var heartbeats = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(heartbeatInterval);
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                await WriteAsync(new ServerMessage { Heartbeat = new Heartbeat { SentAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow) } });
            }
        }, stop.Token);

        try
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                switch (message.BodyCase)
                {
                    case RunnerMessage.BodyOneofCase.Hello:
                        await WriteAsync(new ServerMessage { Welcome = new Welcome { ServerId = "cli" } });
                        break;
                    case RunnerMessage.BodyOneofCase.AllocateNextJob when Interlocked.Exchange(ref handedOut, 1) == 0:
                        await WriteAsync(new ServerMessage { ExecuteJob = job });
                        break;
                    case RunnerMessage.BodyOneofCase.JobStarted:
                        Started.TrySetResult(message.JobStarted.StartedAt.ToDateTimeOffset());
                        break;
                    case RunnerMessage.BodyOneofCase.MarkJobAsFinished:
                        Finished.TrySetResult(message.MarkJobAsFinished);
                        break;
                }
            }
        }
        finally
        {
            await stop.CancelAsync();
            await heartbeats.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
