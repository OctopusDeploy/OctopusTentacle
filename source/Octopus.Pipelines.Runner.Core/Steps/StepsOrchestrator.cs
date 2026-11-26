using System.Text;
using System.Text.Json;
using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.Core.Steps;

/// <summary>Where a backend runs a job's steps: one environment per step, on storage shared with the job runner.</summary>
public interface IStepEnvironment
{
    /// <summary>
    /// Prepares the job: its directory on shared storage and its services. Every step's container mounts the job's
    /// directory at <see cref="StepLayout.MountPath"/>.
    /// </summary>
    Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken);
}

/// <summary>One job's steps on a backend. Disposing it removes the job's services and files.</summary>
public interface IStepSession : IAsyncDisposable
{
    /// <summary>The job's directory on shared storage, as the job runner sees it.</summary>
    string Directory { get; }

    /// <summary>
    /// Runs the step runner for step <paramref name="index"/> in the step's image, streaming its output, and returns
    /// the step runner's exit code. Cancelling stops the step's container.
    /// </summary>
    Task<int> RunStepAsync(int index, StepSpec step, CancellationToken cancellationToken);
}

/// <summary>
/// The job runner's side of a job with steps. Runs one step at a time, each in its own environment: writes the step's
/// input (its definition and the context so far), enforces its timeout, and reads back the result the step runner
/// left on shared storage. Deciding whether a step runs, resolving expressions and masking secrets all happen in the
/// step runner, inside the step's container.
/// </summary>
public sealed class StepsOrchestrator(IStepEnvironment environment, TimeProvider? clock = null)
{
    readonly TimeProvider clock = clock ?? TimeProvider.System;

    /// <summary>Runs the job's steps and returns the first failed step's exit code, or 0.</summary>
    public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        var duplicate = job.Steps.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"The job has more than one step with id '{duplicate.Key}'.");
        }

        var masker = new SecretMasker();
        foreach (var secret in job.Secrets.Values)
        {
            masker.Add(secret);
        }

        var context = new StepContext
        {
            Env = job.Env.ToDictionary(),
            Secrets = job.Secrets.ToDictionary(),
        };
        var log = new MaskedLog(output, masker);
        var stepsOutput = new StartedOnce(output);
        int? firstFailure = null;
        long outputBytes = 0;

        await using var session = await environment.StartAsync(job, stepsOutput, cancellationToken);
        for (var index = 0; index < job.Steps.Count; index++)
        {
            var step = job.Steps[index];
            var directory = PrepareStep(session.Directory, index, Input(job, step, context));

            string? timedOut = null;
            int exitCode;
            using (var timeout = step.Timeout is { } limit ? new CancellationTokenSource(limit, clock) : new CancellationTokenSource())
            using (var stepCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
            {
                try
                {
                    exitCode = await session.RunStepAsync(index, step, stepCancellation.Token);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    exitCode = -1;
                }

                // A step runner that stops gracefully returns rather than throwing, so check whether the step was stopped.
                cancellationToken.ThrowIfCancellationRequested();
                if (timeout.IsCancellationRequested)
                {
                    timedOut = $"The step timed out after {step.Timeout}.";
                }
            }

            // The step runner logs its own errors; these are the ones only the job runner can see.
            var result = ReadResult(directory, step, exitCode, out var broken);
            if (timedOut is not null)
            {
                Fail(result, timedOut, log);
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

            foreach (var mask in result.Masks)
            {
                masker.Add(mask);
                context.Masks.Add(mask);
            }

            context.Steps[step.Id] = new StepContextStep { Outcome = result.Outcome, Outputs = result.Outputs };
            if (result.Outcome == StepRunOutcome.Failure)
            {
                context.Job.Status = JobStatus.Failure;
                firstFailure ??= result.ExitCode is { } code and not 0 ? code : 1;
            }

            output.StepFinished(Report(result, step.Id, masker, log));
        }

        return firstFailure ?? 0;
    }

    static StepInput Input(JobSpec job, StepSpec step, StepContext context) => new()
    {
        Job = new StepJob { Id = job.JobId, RunId = job.RunId },
        Step = new StepDefinition
        {
            Id = step.Id,
            Name = step.Name,
            Image = step.Image,
            If = step.If,
            Shell = step.Shell,
            Env = step.Env.ToDictionary(),
            Run = step.Run,
        },
        Context = context,
    };

    /// <summary>Creates the step's directory, writable by the step's user, with its input and an empty output file.</summary>
    static string PrepareStep(string jobDirectory, int index, StepInput input)
    {
        var directory = Path.Combine(jobDirectory, StepLayout.StepDirectory(index));
        SharedStorage.CreateDirectory(directory);
        File.Delete(Path.Combine(directory, StepLayout.ResultFile));
        File.WriteAllText(Path.Combine(directory, StepLayout.InputFile), StepJsonContext.Write(input));
        var outputFile = Path.Combine(directory, StepLayout.OutputFile);
        File.WriteAllText(outputFile, "");
        SharedStorage.OpenToStep(outputFile);
        return directory;
    }

    static void Fail(StepRunResult result, string error, MaskedLog log)
    {
        result.Outcome = StepRunOutcome.Failure;
        result.Error = error;
        log.Line($"Error: {error}");
    }

    /// <summary>The step runner's result, or an empty one and why, when it left none or an invalid one.</summary>
    static StepRunResult ReadResult(string directory, StepSpec step, int exitCode, out string? broken)
    {
        StepRunResult Broken(string why, out string? reason)
        {
            reason = why;
            return new StepRunResult { StepId = step.Id, Outcome = StepRunOutcome.Failure };
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

        return result.StepId.Equals(step.Id, StringComparison.OrdinalIgnoreCase)
            ? result
            : Broken($"The step runner's result is for step '{result.StepId}', not '{step.Id}'.", out broken);
    }

    /// <summary>What leaves the job: as GitHub Actions does for job outputs, an output that holds a secret stays behind.</summary>
    static StepResult Report(StepRunResult result, string stepId, SecretMasker masker, MaskedLog log)
    {
        var outputs = new Dictionary<string, string>();
        foreach (var (name, value) in result.Outputs)
        {
            if (masker.Contains(value))
            {
                log.Line($"Warning: Skipped output '{name}' of step '{stepId}' since it may contain a secret.");
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
            _ => StepOutcome.Failure,
        };
        return new StepResult(stepId, outcome, result.ExitCode, outputs) { Error = result.Error };
    }

    /// <summary>The job runner's own lines in the job's log, masked like the steps' output.</summary>
    sealed class MaskedLog(IJobOutput output, SecretMasker masker)
    {
        public void Line(string text) => output.Write(Encoding.UTF8.GetBytes(masker.Apply(text) + "\n"));
    }

    /// <summary>Reports the job started when its first step's container does, and only then.</summary>
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

/// <summary>Files on shared storage that a step's container, running as another user, must be able to write.</summary>
public static class SharedStorage
{
    public static void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)0b111_111_111);
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
