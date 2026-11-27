using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Octopus.Pipelines.Runner.Contracts;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// Messages waiting to go to the Server. Survives reconnects, so job state changes that happen
/// while the stream is down are delivered once it's back.
/// </summary>
public sealed class Outbox
{
    readonly Channel<RunnerMessage> channel = Channel.CreateUnbounded<RunnerMessage>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<RunnerMessage> Reader => channel.Reader;

    public void Heartbeat(DateTimeOffset sentAt) =>
        channel.Writer.TryWrite(new RunnerMessage { Heartbeat = new Heartbeat { SentAt = Timestamp.FromDateTimeOffset(sentAt) } });

    public void JobStarted(string jobId, DateTimeOffset startedAt) =>
        channel.Writer.TryWrite(new RunnerMessage
        {
            JobStarted = new JobStartedEvent { JobId = jobId, StartedAt = Timestamp.FromDateTimeOffset(startedAt) },
        });

    public void StepFinished(string jobId, StepResult result) =>
        channel.Writer.TryWrite(new RunnerMessage { StepFinished = new StepFinishedEvent { JobId = jobId, Result = result } });

    public void MarkJobAsFinished(
        string jobId,
        JobOutcome outcome,
        int exitCode,
        DateTimeOffset completedAt,
        long logBytes,
        string? error,
        IEnumerable<StepResult>? steps = null)
    {
        var finished = new MarkJobAsFinishedCommand
        {
            JobId = jobId,
            Outcome = outcome,
            ExitCode = exitCode,
            CompletedAt = Timestamp.FromDateTimeOffset(completedAt),
            LogBytes = logBytes,
            Error = error ?? "",
        };
        if (steps is not null)
        {
            finished.Steps.AddRange(steps);
        }

        channel.Writer.TryWrite(new RunnerMessage { MarkJobAsFinished = finished });
    }
}
