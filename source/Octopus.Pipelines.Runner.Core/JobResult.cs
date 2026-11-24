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
    /// <summary>How each step ended, in order; empty for a single-command job.</summary>
    public IReadOnlyList<StepResult> Steps { get; init; } = [];
}
