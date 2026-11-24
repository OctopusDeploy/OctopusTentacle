using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Core.Steps;

namespace Octopus.Pipelines.Runner.Core;

/// <summary>Runs one job on a backend, streaming its output to a log in the job's directory.</summary>
public sealed class JobRunner(
    IJobBackend backend,
    ILogStore logs,
    JobRunnerOptions options,
    TimeProvider? clock = null,
    ILogger? logger = null)
{
    readonly TimeProvider clock = clock ?? TimeProvider.System;
    readonly ILogger logger = logger ?? NullLogger.Instance;

    /// <summary>
    /// Runs the job and returns once its log is durable. Failures are reported in the result; only the
    /// caller's cancellation is reported as "Runner shutting down.".
    /// </summary>
    public async Task<JobResult> RunAsync(
        JobSpec job,
        Uri jobDirectory,
        IJobObserver? observer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(jobDirectory);

        var succeeded = false;
        var exitCode = -1;
        string? error = null;
        long logBytes = 0;
        DateTimeOffset? startedAt = null;
        var steps = new List<StepResult>();

        try
        {
            await using var log = new StreamingLogWriter(
                await logs.CreateAsync(jobDirectory, JobFiles.Log, cancellationToken),
                options.LogFlushInterval,
                options.LogFlushBytes,
                options.MaxPendingLogBytes,
                logger);

            // A log that can't be stored fails the job promptly: cancel the backend through a token that
            // also fires on the writer's fault.
            using var backendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, log.Faulted);
            try
            {
                var output = new Output(log, observer, at => startedAt = at, steps);
                exitCode = await backend.RunAsync(job, output, backendCts.Token);
                succeeded = exitCode == 0;
            }
            catch (OperationCanceledException) when (log.Faulted.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Stopped because the log failed; CompleteAsync below throws the log storage error.
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !backendCts.IsCancellationRequested)
            {
                // A cancellation that neither the caller nor the log asked for (e.g. an HTTP timeout) is the
                // backend's own failure.
                error = $"{ex.GetType().Name}: {ex.Message}";
                logger.LogWarning(ex, "Job {JobId} failed to run", job.JobId);
            }

            // The job only counts as complete once its log is durable in the storage cell.
            await log.CompleteAsync(cancellationToken);
            logBytes = log.Length;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error = "Runner shutting down.";
            succeeded = false;
        }
        catch (Exception ex)
        {
            error ??= $"Log storage: {ex.GetType().Name}: {ex.Message}";
            succeeded = false;
            logger.LogWarning(ex, "Job {JobId} log storage failed", job.JobId);
        }

        List<StepResult> finishedSteps;
        lock (steps)
        {
            finishedSteps = [.. steps];
        }

        return new JobResult(succeeded, exitCode, logBytes, error, startedAt, clock.GetUtcNow()) { Steps = finishedSteps };
    }

    sealed class Output(StreamingLogWriter log, IJobObserver? observer, Action<DateTimeOffset> recordStarted, List<StepResult> steps) : IJobOutput
    {
        int started;

        public void Started(DateTimeOffset startedAt)
        {
            if (Interlocked.Exchange(ref started, 1) == 0)
            {
                recordStarted(startedAt);
                observer?.Started(startedAt);
            }
        }

        public void WaitingForNode() => observer?.WaitingForNode();

        public void Placed() => observer?.Placed();

        public void Write(ReadOnlySpan<byte> data)
        {
            log.Write(data);
            observer?.Output(data);
        }

        public void StepFinished(StepResult result)
        {
            lock (steps)
            {
                steps.Add(result);
            }

            observer?.StepFinished(result);
        }
    }
}
