using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;
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

    /// <summary>The cancellation handle of each job that's running, for <see cref="Cancel"/>.</summary>
    readonly ConcurrentDictionary<string, JobCancellation> cancellations = new();

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

    /// <summary>
    /// Asks a running job to stop: its running step is stopped, and later steps run only if their conditions allow it
    /// after a cancellation. <paramref name="force"/> stops everything at once. Returns false for a job this runner
    /// isn't running, e.g. one that has already finished.
    /// </summary>
    public bool Cancel(string jobId, bool force)
    {
        if (!cancellations.TryGetValue(jobId, out var cancellation))
        {
            return false;
        }

        logger.LogInformation("Cancelling job {JobId}{Force}", jobId, force ? " (forced)" : "");
        cancellation.Request(force);
        return true;
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
        using var cancellation = new JobCancellation();
        cancellations[jobId] = cancellation;
        try
        {
            var job = new JobSpec(jobId, assignment.RunId, assignment.Command, ToIsolation(assignment.Isolation))
            {
                Steps = assignment.Steps.Select(ToStep).ToArray(),
                Image = assignment.Image.Length > 0 ? assignment.Image : null,
                Services = assignment.Services.Select(s => new ServiceSpec(s.Name, s.Image)
                {
                    Env = new Dictionary<string, string>(s.Env),
                    ReadinessCommand = s.ReadinessCommand.ToArray(),
                }).ToArray(),
                RegistryCredentials = assignment.RegistryCredentials.Select(c => new Core.Steps.RegistryCredential(c.Registry, c.Username, c.Password)).ToArray(),
                Timeout = assignment.TimeoutSeconds > 0 ? TimeSpan.FromSeconds(assignment.TimeoutSeconds) : null,
                Cancellation = cancellation,
            };
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
            cancellations.TryRemove(jobId, out _);
            outbox.MarkJobAsFinished(jobId, ToProto(result.Outcome), result.ExitCode, result.CompletedAt, result.LogBytes, result.Error, result.Steps.Select(ToProto));
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

    static StepSpec ToStep(Step step) => new(step.Id, step.StepRunnerImage, step.Payload)
    {
        Image = step.Image.Length > 0 ? step.Image : null,
        Timeout = step.TimeoutSeconds > 0 ? TimeSpan.FromSeconds(step.TimeoutSeconds) : null,
    };

    static Contracts.JobOutcome ToProto(Core.JobOutcome outcome) => outcome switch
    {
        Core.JobOutcome.Succeeded => Contracts.JobOutcome.Succeeded,
        Core.JobOutcome.Failed => Contracts.JobOutcome.Failed,
        Core.JobOutcome.Cancelled => Contracts.JobOutcome.Cancelled,
        _ => Contracts.JobOutcome.Errored,
    };

    static Contracts.StepResult ToProto(Core.Steps.StepResult result)
    {
        var proto = new Contracts.StepResult
        {
            Id = result.Id,
            Error = result.Error ?? "",
            Outcome = result.Outcome switch
            {
                Core.Steps.StepOutcome.Success => Contracts.StepOutcome.Success,
                Core.Steps.StepOutcome.Failure => Contracts.StepOutcome.Failure,
                Core.Steps.StepOutcome.Cancelled => Contracts.StepOutcome.Cancelled,
                _ => Contracts.StepOutcome.Skipped,
            },
            StartedAt = result.StartedAt is { } started ? Timestamp.FromDateTimeOffset(started) : null,
            FinishedAt = result.FinishedAt is { } finished ? Timestamp.FromDateTimeOffset(finished) : null,
        };
        foreach (var (name, value) in result.Outputs)
        {
            proto.Outputs[name] = value;
        }

        if (result.ExitCode is { } exitCode)
        {
            proto.ExitCode = exitCode;
        }

        return proto;
    }

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

        public void StepFinished(Core.Steps.StepResult result) => outbox.StepFinished(jobId, ToProto(result));
    }
}
