namespace Octopus.Pipelines.Runner.Core.Steps;

/// <summary>
/// One step of a job, as the Server sends it. The job runner reads only what it needs to run the step; everything
/// about what the step does is in <see cref="Payload"/>, which goes to the step runner untouched.
/// </summary>
/// <param name="Id">Unique within the job. The job runner reports the step's result under it.</param>
/// <param name="StepRunnerImage">The image the step runner comes from. The Server picks a version that reads <paramref name="Payload"/>.</param>
/// <param name="Payload">For the step runner only: the job runner never reads it.</param>
public sealed record StepSpec(string Id, string StepRunnerImage, string Payload)
{
    /// <summary>The image the step runs in, or null for the job's.</summary>
    public string? Image { get; init; }

    /// <summary>How long the step may run before the job runner stops it and fails it. Null means no limit.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>A container that runs alongside every step of a job, reachable from the steps by <paramref name="Name"/>.</summary>
/// <param name="Image">Runs with its own entrypoint and command.</param>
public sealed record ServiceSpec(string Name, string Image)
{
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();

    /// <summary>A command run in the service's container until it succeeds; steps start after that. Empty means "once running".</summary>
    public IReadOnlyList<string> ReadinessCommand { get; init; } = [];
}

/// <summary>Credentials for pulling a job's images from a private registry, e.g. <c>ghcr.io</c>.</summary>
public sealed record RegistryCredential(string Registry, string Username, string Password);

public enum StepOutcome
{
    Success,
    Failure,
    Skipped,

    /// <summary>Stopped before it finished, by a timeout or a cancellation.</summary>
    Cancelled,
}

/// <param name="ExitCode">The script's exit code, or null when it didn't run.</param>
/// <param name="Outputs">What the step wrote to <c>$OCTOPUS_OUTPUT</c>, less any output that contains a secret: those stay inside the job.</param>
public sealed record StepResult(string Id, StepOutcome Outcome, int? ExitCode, IReadOnlyDictionary<string, string> Outputs)
{
    /// <summary>Why the step failed other than by its script's exit code, or null.</summary>
    public string? Error { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }
}
