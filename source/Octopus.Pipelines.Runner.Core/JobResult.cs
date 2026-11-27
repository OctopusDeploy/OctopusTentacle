using Octopus.Pipelines.Runner.Core.Steps;

namespace Octopus.Pipelines.Runner.Core;

/// <summary>How a job ended. The job's log is durable by the time a result is returned.</summary>
/// <param name="Error">Why the job failed other than by exiting non-zero, or null.</param>
public sealed record JobResult(
    bool Succeeded,
    int ExitCode,
    long LogBytes,
    string? Error,
    DateTimeOffset? StartedAt,
    DateTimeOffset CompletedAt)
{
    readonly JobOutcome? outcome;

    /// <summary>How each step ended, in order; empty for a single-command job.</summary>
    public IReadOnlyList<StepResult> Steps { get; init; } = [];

    /// <summary>Set for a cancelled job; otherwise worked out from <see cref="Error"/> and <see cref="Succeeded"/>.</summary>
    public JobOutcome Outcome
    {
        get => outcome ?? (Error is not null ? JobOutcome.Errored : Succeeded ? JobOutcome.Succeeded : JobOutcome.Failed);
        init => outcome = value;
    }
}

public enum JobOutcome
{
    Succeeded,

    /// <summary>The job's command, or one of its steps, failed.</summary>
    Failed,

    /// <summary>Stopped by a cancellation request or the job's timeout.</summary>
    Cancelled,

    /// <summary>The runner couldn't run the job: a backend or log storage failure, or the runner shutting down.</summary>
    Errored,
}
