using Octopus.Pipelines.Runner.Core.Steps;

namespace Octopus.Pipelines.Runner.Core;

public enum JobIsolation
{
    Standard,
    Sandboxed,
}

/// <param name="Command">The shell command of a single-command job; ignored when the job has <see cref="Steps"/>.</param>
public sealed record JobSpec(string JobId, string RunId, string Command, JobIsolation Isolation = JobIsolation.Standard)
{
    public bool Sandboxed => Isolation == JobIsolation.Sandboxed;

    /// <summary>The job's steps, run in order, or empty for a single-command job.</summary>
    public IReadOnlyList<StepSpec> Steps { get; init; } = [];
}

/// <summary>Callbacks a backend uses while a job runs.</summary>
public interface IJobOutput
{
    /// <summary>The job's process has started. Called at most once.</summary>
    void Started(DateTimeOffset startedAt);

    /// <summary>
    /// The job is waiting for room on a node, e.g. while the cluster adds one. Time spent like this isn't
    /// counted as a slow start: it's capacity being added, not the cell struggling to start pods.
    /// </summary>
    void WaitingForNode();

    /// <summary>The job has a node to run on.</summary>
    void Placed();

    /// <summary>Raw stdout/stderr bytes, in order, as they're produced.</summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>A step finished or was skipped. Called once per step, in step order, before the job returns.</summary>
    void StepFinished(StepResult result)
    {
    }
}

/// <summary>The scaling unit that provides compute for jobs inside a runner cell.</summary>
public interface IJobBackend
{
    /// <summary>Runs the job to completion, streaming its output, and returns the exit code.</summary>
    Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken);
}
