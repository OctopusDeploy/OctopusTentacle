using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using CoreIsolation = Octopus.Pipelines.Runner.Core.JobIsolation;
using ProtoIsolation = Octopus.Pipelines.Runner.Contracts.JobIsolation;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>Runs assigned jobs and reports their lifecycle through the <see cref="Outbox"/>.</summary>
public sealed class GatewayJobExecutor(
    JobRunner runner,
    Outbox outbox,
    CapacitySignal capacityChanged,
    TimeProvider clock,
    IHostApplicationLifetime lifetime,
    ILogger<GatewayJobExecutor> logger)
{
    readonly ConcurrentDictionary<string, Task> inFlight = new();

    /// <summary>Jobs in flight that haven't started yet.</summary>
    readonly ConcurrentDictionary<string, StartingJob> starting = new();

    public IReadOnlyCollection<string> InFlightJobIds => inFlight.Keys.ToArray();

    public int InFlightCount => inFlight.Count;

    /// <summary>Jobs in flight whose backend hasn't reported them started yet, e.g. pods still being scheduled or set up.</summary>
    public int StartingCount => starting.Count;

    /// <summary>
    /// When each starting job's start delay began, as <see cref="TimeProvider.GetTimestamp"/> values: its
    /// assignment, or when it got a node if it had to wait for one. Jobs waiting for a node are left out.
    /// </summary>
    public IReadOnlyCollection<long> StartingSince =>
        starting.Values.Select(job => job.Since).OfType<long>().ToArray();

    /// <summary>
    /// A job started: (start delay began, started), as <see cref="TimeProvider.GetTimestamp"/> values.
    /// Time spent waiting for a node isn't part of the delay.
    /// </summary>
    public event Action<long, long>? JobStartObserved;

    public void Start(ExecuteJobCommand assignment)
    {
        var started = new TaskCompletionSource();
        var task = RunAsync(assignment, started.Task);
        if (inFlight.TryAdd(assignment.JobId, task))
        {
            starting[assignment.JobId] = new StartingJob(clock.GetTimestamp());
            started.SetResult();
        }
        else
        {
            logger.LogWarning("Ignoring duplicate assignment for job {JobId}", assignment.JobId);
            started.SetCanceled();
        }
    }

    /// <summary>Waits for in-flight jobs to finish, e.g. while draining the cell.</summary>
    public Task WhenAllCompleted() => Task.WhenAll(inFlight.Values);

    async Task RunAsync(ExecuteJobCommand assignment, Task gate)
    {
        try
        {
            await gate;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var jobId = assignment.JobId;
        var result = new JobResult(false, -1, 0, null, null, clock.GetUtcNow());
        try
        {
            var job = new JobSpec(jobId, assignment.RunId, assignment.Command, ToIsolation(assignment.Isolation));
            result = await runner.RunAsync(job, new Uri(assignment.Storage.DirectoryUri), new Observer(this, jobId, outbox), lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            // JobRunner reports failures in its result; this is for what escapes it, e.g. a malformed directory URI.
            logger.LogWarning(ex, "Job {JobId} could not be run", jobId);
            result = new JobResult(false, -1, 0, $"{ex.GetType().Name}: {ex.Message}", null, clock.GetUtcNow());
        }
        finally
        {
            outbox.MarkJobAsFinished(jobId, result.Succeeded, result.ExitCode, result.CompletedAt, result.LogBytes, result.Error);
            starting.TryRemove(jobId, out _);
            inFlight.TryRemove(jobId, out _);
            capacityChanged.Notify();
        }
    }

    void OnStarted(string jobId)
    {
        if (starting.TryRemove(jobId, out var job) && job.Since is { } since)
        {
            JobStartObserved?.Invoke(since, clock.GetTimestamp());
            // A starting slot freed up, so a capped provider may want more work now.
            capacityChanged.Notify();
        }
    }

    // Unspecified comes from a Server that predates isolation, which only ever meant standard.
    static CoreIsolation ToIsolation(ProtoIsolation isolation) => isolation switch
    {
        ProtoIsolation.Sandboxed => CoreIsolation.Sandboxed,
        _ => CoreIsolation.Standard,
    };

    void OnWaitingForNode(string jobId)
    {
        if (starting.TryGetValue(jobId, out var job))
        {
            job.WaitForNode();
        }
    }

    void OnPlaced(string jobId)
    {
        if (starting.TryGetValue(jobId, out var job))
        {
            job.Place(clock.GetTimestamp());
        }
    }

    /// <summary>A job that hasn't started yet, and when its start delay began.</summary>
    sealed class StartingJob(long assignedAt)
    {
        readonly Lock gate = new();
        long since = assignedAt;
        bool waitingForNode;

        /// <summary>When the start delay began, or null while the job is waiting for a node.</summary>
        public long? Since
        {
            get
            {
                lock (gate)
                {
                    return waitingForNode ? null : since;
                }
            }
        }

        public void WaitForNode()
        {
            lock (gate)
            {
                waitingForNode = true;
            }
        }

        /// <summary>Restarts the delay from now if the job was waiting for a node.</summary>
        public void Place(long now)
        {
            lock (gate)
            {
                if (waitingForNode)
                {
                    waitingForNode = false;
                    since = now;
                }
            }
        }
    }

    sealed class Observer(GatewayJobExecutor owner, string jobId, Outbox outbox) : IJobObserver
    {
        public void Started(DateTimeOffset startedAt)
        {
            outbox.JobStarted(jobId, startedAt);
            owner.OnStarted(jobId);
        }

        public void WaitingForNode() => owner.OnWaitingForNode(jobId);

        public void Placed() => owner.OnPlaced(jobId);
    }
}
