namespace Octopus.Pipelines.Runner.Core.Steps;

/// <summary>
/// One step of a job, in the shape of a GitHub Actions <c>run</c> step. <see cref="Run"/>, <see cref="If"/>,
/// <see cref="Name"/> and the <see cref="Env"/> values may hold <c>${{ }}</c> expressions, which the step runner
/// resolves inside the step's container.
/// </summary>
/// <param name="Id">Unique within the job; expressions refer to the step as <c>steps.&lt;id&gt;</c>.</param>
/// <param name="Run">The script. The image's own entrypoint doesn't run.</param>
public sealed record StepSpec(string Id, string Run)
{
    public string? Name { get; init; }

    /// <summary>The step's condition, with or without <c>${{ }}</c>. Null means <c>success()</c>.</summary>
    public string? If { get; init; }

    /// <summary>The image the step runs in, or null for the backend's default.</summary>
    public string? Image { get; init; }

    /// <summary>How the script is run, as GitHub Actions' <c>shell:</c>. Null means bash if the image has it, else sh.</summary>
    public string? Shell { get; init; }

    /// <summary>How long the step may run before the job runner stops it and fails it. Null means no limit.</summary>
    public TimeSpan? Timeout { get; init; }

    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();

    public string DisplayName => Name ?? Id;
}

/// <summary>A container that runs alongside every step of a job, reachable from the steps by <paramref name="Name"/>.</summary>
/// <param name="Image">Runs with its own entrypoint and command.</param>
public sealed record ServiceSpec(string Name, string Image)
{
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
}

public enum StepOutcome
{
    Success,
    Failure,
    Skipped,
}

/// <param name="ExitCode">The script's exit code, or null when it didn't run.</param>
/// <param name="Outputs">
/// What the step wrote to <c>$OCTOPUS_OUTPUT</c>, less any output that contains a secret: those stay inside the job.
/// </param>
public sealed record StepResult(string Id, StepOutcome Outcome, int? ExitCode, IReadOnlyDictionary<string, string> Outputs)
{
    /// <summary>Why the step failed other than by its script's exit code, or null.</summary>
    public string? Error { get; init; }

    public static StepResult Skipped(string id) => new(id, StepOutcome.Skipped, null, new Dictionary<string, string>());
}
