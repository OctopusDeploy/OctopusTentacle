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

    /// <summary>The image of steps that don't name one, or null for the backend's default.</summary>
    public string? Image { get; init; }

    /// <summary>Containers that run alongside every step, reachable by name.</summary>
    public IReadOnlyList<ServiceSpec> Services { get; init; } = [];

    /// <summary>Credentials for the private registries the job's images come from.</summary>
    public IReadOnlyList<RegistryCredential> RegistryCredentials { get; init; } = [];

    /// <summary>How long the whole job may run before it's cancelled. Null means no limit.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Lets the Server cancel the job while it runs.</summary>
    public JobCancellation Cancellation { get; init; } = new();

    /// <summary>The image a step runs in, as a key: its own, else the job's, else "" for the backend's default.</summary>
    public string ImageFor(StepSpec step) => step.Image ?? Image ?? "";

    /// <summary>The job's step runner images, each once, in order of first use. A step runner's key is its index here.</summary>
    public IReadOnlyList<string> StepRunnerImages => Steps.Select(s => s.StepRunnerImage).Distinct().ToArray();
}

/// <summary>
/// A request to stop a running job. A request stops the running step, and later steps run only if their conditions
/// allow it after a cancellation (e.g. <c>always()</c>), as in GitHub Actions. A forced request stops everything at once.
/// </summary>
public sealed class JobCancellation : IDisposable
{
    readonly CancellationTokenSource requested = new();
    readonly CancellationTokenSource forced = new();

    public CancellationToken Requested => requested.Token;

    public CancellationToken Forced => forced.Token;

    public bool IsRequested => requested.IsCancellationRequested;

    public bool IsForced => forced.IsCancellationRequested;

    /// <summary>Why the job was cancelled, e.g. a timeout; null for a cancellation the Server asked for.</summary>
    public string? Reason { get; private set; }

    public void Request(bool force = false, string? reason = null)
    {
        Reason ??= reason;
        requested.Cancel();
        if (force)
        {
            forced.Cancel();
        }
    }

    public void Dispose()
    {
        requested.Dispose();
        forced.Dispose();
    }
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
