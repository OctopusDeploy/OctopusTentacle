using System.Collections.Concurrent;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Octopus.Pipelines.Runner.Contracts;

namespace Octopus.Pipelines.Runner.CliSample;

/// <summary>
/// The CLI's side of the RunnerGateway contract: it plays the Server, queueing jobs and handing them out as the runner
/// asks for them. The runner connects to it in process (see <c>InProcessCallInvoker</c>), so jobs go through the same
/// gateway code as on the remote hosts.
/// </summary>
sealed class CliJobSource(TimeSpan heartbeatInterval) : RunnerGateway.RunnerGatewayBase
{
    readonly Channel<ExecuteJobCommand> queue = Channel.CreateUnbounded<ExecuteJobCommand>();
    readonly ConcurrentDictionary<string, TaskCompletionSource<MarkJobAsFinishedCommand>> finished = new();

    /// <summary>Queues a job; completes when the runner reports it finished, by which time its log is durable.</summary>
    public Task<MarkJobAsFinishedCommand> RunAsync(ExecuteJobCommand job)
    {
        var done = new TaskCompletionSource<MarkJobAsFinishedCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        finished[job.JobId] = done;
        queue.Writer.TryWrite(job);
        return done.Task;
    }

    public override async Task Connect(IAsyncStreamReader<RunnerMessage> requestStream, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
    {
        // The heartbeat, the dispatcher and the replies below all write to the stream, which allows one write at a time.
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

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        // Like the Server, keep the stream busy: the runner reconnects if it hears nothing for a while.
        var heartbeats = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(heartbeatInterval);
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                await WriteAsync(new ServerMessage { Heartbeat = new Heartbeat { SentAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow) } });
            }
        }, stop.Token);

        // Allocation requests add up and are held until there's a job for each, like a long poll.
        using var requested = new SemaphoreSlim(0);
        var dispatcher = Task.Run(async () =>
        {
            while (true)
            {
                await requested.WaitAsync(stop.Token);
                var job = await queue.Reader.ReadAsync(stop.Token);
                await WriteAsync(new ServerMessage { ExecuteJob = job });
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
                    case RunnerMessage.BodyOneofCase.AllocateNextJob when message.AllocateNextJob.Count > 0:
                        requested.Release(message.AllocateNextJob.Count);
                        break;
                    case RunnerMessage.BodyOneofCase.MarkJobAsFinished:
                        if (finished.TryRemove(message.MarkJobAsFinished.JobId, out var done))
                        {
                            done.TrySetResult(message.MarkJobAsFinished);
                        }

                        break;
                }
            }
        }
        finally
        {
            await stop.CancelAsync();
            foreach (var task in new[] { heartbeats, dispatcher })
            {
                await task.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }
}
