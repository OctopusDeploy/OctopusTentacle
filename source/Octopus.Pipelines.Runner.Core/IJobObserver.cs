using Octopus.Pipelines.Runner.Core.Steps;

namespace Octopus.Pipelines.Runner.Core;

/// <summary>Optional listener for a job's lifecycle while <see cref="JobRunner"/> runs it.</summary>
/// <remarks>
/// Methods may be called from any thread, including a backend's output pumps, so they must be thread safe and fast:
/// a slow observer holds up the job's output. They must not throw. An exception from any of them, <see cref="Output"/>
/// included, escapes into the backend and fails the job as a backend error.
/// </remarks>
public interface IJobObserver
{
    /// <summary>The job's process has started. Called at most once.</summary>
    void Started(DateTimeOffset startedAt)
    {
    }

    /// <summary>The job is waiting for room on a node.</summary>
    void WaitingForNode()
    {
    }

    /// <summary>The job has a node to run on.</summary>
    void Placed()
    {
    }

    /// <summary>The same raw output bytes that go to the job's log.</summary>
    void Output(ReadOnlySpan<byte> data)
    {
    }

    /// <summary>A step finished or was skipped.</summary>
    void StepFinished(StepResult result)
    {
    }
}
