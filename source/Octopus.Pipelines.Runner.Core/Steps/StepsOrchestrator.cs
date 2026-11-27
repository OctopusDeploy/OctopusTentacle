using System.Text;
using System.Text.Json;
using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.Core.Steps;

/// <summary>Where a backend runs a job's steps: a long-lived container per image, with steps run in it by exec.</summary>
public interface IStepEnvironment
{
    /// <summary>
    /// Prepares the job: pulls its images, installs its step runners, creates its directory on shared storage (see
    /// <see cref="StepLayout"/>) and starts its services, waiting until each is ready.
    /// </summary>
    Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken);
}

/// <summary>One job's containers on a backend. Disposing it removes the job's containers, services and files.</summary>
public interface IStepSession : IAsyncDisposable
{
    /// <summary>The job's directory on shared storage, as the job runner sees it.</summary>
    string Directory { get; }

    /// <summary>
    /// Runs <paramref name="command"/> in the job's container for <paramref name="image"/> (see <see cref="JobSpec.ImageFor"/>),
    /// creating it first if there isn't one, streams its output, and returns its exit code. Cancelling stops the
    /// container outright, and a later step needing the image gets a new one.
    /// </summary>
    Task<int> ExecAsync(string image, IReadOnlyList<string> command, CancellationToken cancellationToken);

    /// <summary>Removes the job's container for <paramref name="image"/>, once no later step needs it.</summary>
    Task ReleaseAsync(string image, CancellationToken cancellationToken);
}

/// <summary>
/// The job runner's side of a job with steps. Runs one step at a time, by exec, in a container kept per image. It
/// writes each step's payload as received, passes the results of the earlier steps, enforces timeouts, and reads back
/// the result the step runner leaves on shared storage. Whether a step runs, what its expressions resolve to and which
/// values to mask are all decided by the step runner: the job runner never reads a payload.
/// </summary>
public sealed class StepsOrchestrator(IStepEnvironment environment, TimeProvider? clock = null)
{
    readonly TimeProvider clock = clock ?? TimeProvider.System;

    /// <summary>How long a step gets to stop, after its cancel file appears, before its container is stopped outright.</summary>
    public TimeSpan StopGracePeriod { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Runs the job's steps and returns the first failed step's exit code, or 0.</summary>
    public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        if (job.Steps.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Id)) is not null)
        {
            throw new InvalidOperationException("Every step needs an id.");
        }

        var duplicate = job.Steps.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"The job has more than one step with id '{duplicate.Key}'.");
        }

        var stepRunners = job.StepRunnerImages;
        var lastUse = job.Steps.Select((step, index) => (Image: job.ImageFor(step), Index: index))
            .GroupBy(s => s.Image)
            .ToDictionary(g => g.Key, g => g.Max(s => s.Index));

        using var jobTimeout = new CancellationTokenSource();
        await using var timeoutRegistration = jobTimeout.Token.Register(() => job.Cancellation.Request(reason: $"The job timed out after {job.Timeout}."));
        if (job.Timeout is { } limit)
        {
            jobTimeout.CancelAfter(limit);
        }

        var log = new Log(output);
        var startedOnce = new StartedOnce(output);
        int? firstFailure = null;
        long outputBytes = 0;

        await using var session = await environment.StartAsync(job, startedOnce, cancellationToken);
        var resultsFile = Path.Combine(session.Directory, StepLayout.ResultsDirectory, StepLayout.ResultsFile);
        for (var index = 0; index < job.Steps.Count; index++)
        {
            if (job.Cancellation.IsForced)
            {
                break;
            }

            var step = job.Steps[index];
            var image = job.ImageFor(step);
            var directory = PrepareStep(session.Directory, index, step);
            var status = job.Cancellation.IsRequested ? JobStatus.Cancelled : firstFailure is not null ? JobStatus.Failure : JobStatus.Success;
            var command = StepLayout.RunCommand(index, IndexOf(stepRunners, step.StepRunnerImage), status);

            var cancelledBefore = job.Cancellation.IsRequested;
            var (exitCode, stopped) = await RunStepAsync(session, image, command, step, directory, job.Cancellation, cancellationToken);
            var result = ReadResult(directory, step, exitCode, out var broken);
            if (broken is not null)
            {
                // Without a result, nobody knows what's still running in the container (e.g. the exec's client died,
                // but the step runner didn't): no later step may share it, so stop it.
                await session.ReleaseAsync(image, cancellationToken);
            }

            if (stopped == Stop.Timeout)
            {
                Fail(result, $"The step timed out after {step.Timeout}.", log);
            }
            else if (broken is not null && (stopped == Stop.Cancelled || (!cancelledBefore && job.Cancellation.IsRequested)))
            {
                // Cancelled while it ran: a missing result is what stopping it looks like.
                result.Outcome = StepRunOutcome.Cancelled;
                result.Error = "The step was stopped before it finished.";
                log.Line($"Error: {result.Error}");
            }
            else if (broken is not null)
            {
                Fail(result, broken, log);
            }

            outputBytes += result.Outputs.Values.Sum(v => (long)Encoding.UTF8.GetByteCount(v));
            if (outputBytes > StepEnvironment.MaxJobOutputBytes)
            {
                result.Outputs.Clear();
                Fail(result, $"The job's outputs add up to more than {StepEnvironment.MaxJobOutputBytes / 1024 / 1024} MB.", log);
            }

            // Later steps read this to evaluate their conditions and expressions. Steps can't write it.
            await File.AppendAllTextAsync(resultsFile, StepJsonContext.Write(result) + "\n", CancellationToken.None);
            if (result.Outcome == StepRunOutcome.Failure)
            {
                firstFailure ??= result.ExitCode is { } code and not 0 ? code : 1;
            }

            output.StepFinished(Report(result, log));

            if (lastUse[image] == index && !job.Cancellation.IsForced)
            {
                await session.ReleaseAsync(image, cancellationToken);
            }
        }

        return firstFailure ?? 0;
    }

    enum Stop
    {
        None,
        Timeout,
        Cancelled,
    }

    /// <summary>
    /// Runs the step runner, and stops it for the step's timeout or a cancellation that arrives while it runs: first by
    /// creating its cancel file, then, after <see cref="StopGracePeriod"/>, by stopping its container. A step that
    /// starts after a cancellation isn't stopped by it: it's one the step runner decided should still run.
    /// </summary>
    async Task<(int ExitCode, Stop Stopped)> RunStepAsync(
        IStepSession session,
        string image,
        IReadOnlyList<string> command,
        StepSpec step,
        string directory,
        JobCancellation cancellation,
        CancellationToken cancellationToken)
    {
        using var hard = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellation.Forced);
        using var timeout = new CancellationTokenSource();
        if (step.Timeout is { } limit)
        {
            timeout.CancelAfter(limit);
        }

        var stopRequested = new TaskCompletionSource<Stop>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var onTimeout = timeout.Token.Register(() => stopRequested.TrySetResult(Stop.Timeout));
        await using var onCancel = cancellation.IsRequested
            ? default
            : cancellation.Requested.Register(() => stopRequested.TrySetResult(Stop.Cancelled));

        var run = session.ExecAsync(image, command, hard.Token);
        var stopped = Stop.None;
        if (await Task.WhenAny(run, stopRequested.Task) != run)
        {
            stopped = await stopRequested.Task;
            await File.WriteAllTextAsync(Path.Combine(directory, StepLayout.CancelFile), stopped.ToString(), CancellationToken.None);
            if (await Task.WhenAny(run, Task.Delay(StopGracePeriod, clock, cancellationToken)) != run)
            {
                await hard.CancelAsync();
            }
        }

        try
        {
            return (await run, stopped);
        }
        catch (OperationCanceledException) when (hard.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The container was stopped outright: a forced cancellation, or a step that didn't stop when asked.
            return (-1, stopped == Stop.None ? Stop.Cancelled : stopped);
        }
    }

    static int IndexOf(IReadOnlyList<string> images, string image)
    {
        for (var i = 0; i < images.Count; i++)
        {
            if (images[i] == image)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Unknown step runner image '{image}'.");
    }

    /// <summary>Creates the step's directory, writable by the step's user, with its payload and an empty output file.</summary>
    static string PrepareStep(string jobDirectory, int index, StepSpec step)
    {
        var directory = Path.Combine(jobDirectory, StepLayout.StepDirectory(index));
        SharedStorage.CreateDirectory(directory);
        File.Delete(Path.Combine(directory, StepLayout.ResultFile));
        File.Delete(Path.Combine(directory, StepLayout.CancelFile));
        File.WriteAllText(Path.Combine(directory, StepLayout.InputFile), step.Payload);
        var outputFile = Path.Combine(directory, StepLayout.OutputFile);
        File.WriteAllText(outputFile, "");
        SharedStorage.OpenToStep(outputFile);
        return directory;
    }

    static void Fail(StepRunResult result, string error, Log log)
    {
        result.Outcome = StepRunOutcome.Failure;
        result.Error = error;
        log.Line($"Error: {error}");
    }

    /// <summary>The step runner's result, or a stand-in failure and why, when it left none or an invalid one.</summary>
    static StepRunResult ReadResult(string directory, StepSpec step, int exitCode, out string? broken)
    {
        StepRunResult Broken(string why, out string? reason)
        {
            reason = why;
            return new StepRunResult { Id = step.Id, Outcome = StepRunOutcome.Failure };
        }

        broken = null;
        var path = Path.Combine(directory, StepLayout.ResultFile);
        if (!File.Exists(path))
        {
            return Broken(
                exitCode == -1 ? "The step was stopped before it finished." : $"The step runner exited {exitCode} without a result.",
                out broken);
        }

        StepRunResult result;
        try
        {
            result = StepJsonContext.ReadResult(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            return Broken($"The step runner's result is invalid: {e.Message}", out broken);
        }

        if (result.Version != StepRunResult.CurrentVersion)
        {
            return Broken($"The step runner's result is version {result.Version}; this job runner reads version {StepRunResult.CurrentVersion}.", out broken);
        }

        // The id is the job runner's, from the Server: the step runner's copy comes from the payload.
        result.Id = step.Id;
        return result;
    }

    /// <summary>What leaves the job. Outputs the step runner withheld (they hold secrets) stay inside it.</summary>
    static StepResult Report(StepRunResult result, Log log)
    {
        var outputs = new Dictionary<string, string>();
        foreach (var (name, value) in result.Outputs)
        {
            if (result.WithheldOutputs.Contains(name))
            {
                log.Line($"Warning: Skipped output '{name}' of step '{result.Id}' since it may contain a secret.");
            }
            else
            {
                outputs[name] = value;
            }
        }

        var outcome = result.Outcome switch
        {
            StepRunOutcome.Success => StepOutcome.Success,
            StepRunOutcome.Skipped => StepOutcome.Skipped,
            StepRunOutcome.Cancelled => StepOutcome.Cancelled,
            _ => StepOutcome.Failure,
        };
        return new StepResult(result.Id, outcome, result.ExitCode, outputs)
        {
            Error = result.Error,
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt,
        };
    }

    /// <summary>The job runner's own lines in the job's log.</summary>
    sealed class Log(IJobOutput output)
    {
        public void Line(string text) => output.Write(Encoding.UTF8.GetBytes(text + "\n"));
    }

    /// <summary>Reports the job started when its first container does, and only then.</summary>
    sealed class StartedOnce(IJobOutput inner) : IJobOutput
    {
        int started;

        public void Started(DateTimeOffset startedAt)
        {
            if (Interlocked.Exchange(ref started, 1) == 0)
            {
                inner.Started(startedAt);
            }
        }

        public void WaitingForNode() => inner.WaitingForNode();

        public void Placed() => inner.Placed();

        public void Write(ReadOnlySpan<byte> data) => inner.Write(data);

        public void StepFinished(StepResult result) => inner.StepFinished(result);
    }
}

/// <summary>Files on shared storage that a job's containers, running as another user, must be able to read or write.</summary>
public static class SharedStorage
{
    /// <summary>Creates a job's directory and the directories its containers mount (see <see cref="StepLayout"/>).</summary>
    public static void CreateJobDirectory(string path)
    {
        CreateDirectory(path);
        CreateDirectory(Path.Combine(path, StepLayout.WorkspaceDirectory));
        CreateDirectory(Path.Combine(path, StepLayout.StepsDirectory));
        CreateDirectory(Path.Combine(path, StepLayout.ResultsDirectory), writableByStep: false);
        File.WriteAllText(Path.Combine(path, StepLayout.ResultsDirectory, StepLayout.ResultsFile), "");
    }

    public static void CreateDirectory(string path, bool writableByStep = true)
    {
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)(writableByStep ? 0b111_111_111 : 0b111_101_101));
        }
    }

    public static void OpenToStep(string file)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(file, (UnixFileMode)0b110_110_110);
        }
    }

    /// <summary>
    /// Deletes a job's directory. Directories a step created in the workspace belong to the step's user, so the
    /// runner may not be able to empty them; returns false then.
    /// </summary>
    public static bool TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
