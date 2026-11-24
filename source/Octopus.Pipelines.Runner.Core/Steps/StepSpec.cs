namespace Octopus.Pipelines.Runner.Core.Steps;

/// <summary>
/// One step of a job, in the shape of a GitHub Actions <c>run</c> step. <see cref="Run"/>, <see cref="If"/> and the
/// <see cref="Env"/> values may hold <c>${{ }}</c> expressions; whoever orchestrates the steps resolves them.
/// </summary>
/// <param name="Id">Unique within the job; expressions refer to the step as <c>steps.&lt;id&gt;</c>.</param>
/// <param name="Run">The script, run with <c>sh -e</c>.</param>
public sealed record StepSpec(string Id, string Run)
{
    public string? Name { get; init; }

    /// <summary>The step's condition, with or without <c>${{ }}</c>. Null means <c>success()</c>.</summary>
    public string? If { get; init; }

    /// <summary>The image the step runs in, or null for the backend's default.</summary>
    public string? Image { get; init; }

    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();

    public string DisplayName => Name ?? Id;
}

public enum StepOutcome
{
    Success,
    Failure,
    Skipped,
}

/// <param name="ExitCode">The script's exit code, or null when it didn't run.</param>
/// <param name="Outputs">What the step wrote to <c>$GITHUB_OUTPUT</c>.</param>
public sealed record StepResult(string Id, StepOutcome Outcome, int? ExitCode, IReadOnlyDictionary<string, string> Outputs)
{
    public static StepResult Skipped(string id) => new(id, StepOutcome.Skipped, null, new Dictionary<string, string>());
}
