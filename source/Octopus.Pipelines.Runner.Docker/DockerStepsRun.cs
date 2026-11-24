using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>
/// Runs one job's steps. Every container gets the job's state directory at <c>/octopus</c>:
/// <code>
/// /octopus/steps/&lt;i&gt;/run.sh    the step's script, run with sh -e
/// /octopus/steps/&lt;i&gt;/output    $GITHUB_OUTPUT
/// /octopus/workspace/          the working directory, shared by every step
/// </code>
/// and the same hardening as a single-command job, plus a tmpfs at <c>/tmp</c>.
/// </summary>
sealed partial class DockerStepsRun(DockerJobBackend backend, JobSpec job, IJobOutput output)
{
    const string Mount = "/octopus";
    const string Workspace = Mount + "/workspace";

    readonly Lock gate = new();
    readonly DockerBackendOptions options = backend.Options;
    string state = "";
    int started;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        foreach (var image in job.Steps.Select(ImageOf).Distinct())
        {
            await backend.EnsureImageAsync(image, cancellationToken);
        }

        state = CreateStateDirectory();
        try
        {
            return options.StepMode switch
            {
                DockerStepMode.Run => await OrchestrateAsync(RunStepAsync, cancellationToken),
                DockerStepMode.Exec => await ExecAsync(cancellationToken),
                DockerStepMode.InContainer => await InContainerAsync(cancellationToken),
                _ => throw new NotSupportedException($"Unknown step mode {options.StepMode}."),
            };
        }
        finally
        {
            DeleteStateDirectory();
        }
    }

    // ---- Orchestrated by the runner -------------------------------------------------------------------------------

    delegate Task<int> StepRunner(int index, StepSpec step, IReadOnlyDictionary<string, string> env, CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates each step's condition, resolves its expressions against the steps before it, runs it, and reads its
    /// outputs back, all here on the runner. Returns the first failed step's exit code, or 0.
    /// </summary>
    async Task<int> OrchestrateAsync(StepRunner runStep, CancellationToken cancellationToken)
    {
        var context = new StepsContext();
        int? firstFailure = null;
        for (var i = 0; i < job.Steps.Count; i++)
        {
            var step = job.Steps[i];
            if (!ExprEvaluator.IsTrue(Expr.ParseCondition(step.If), context))
            {
                Banner($"==> {step.Id}: {step.DisplayName} skipped");
                Finish(context, StepResult.Skipped(step.Id));
                continue;
            }

            // Textual substitution, as GitHub Actions does: a value lands in the script verbatim. Pass untrusted
            // values through env instead.
            await File.WriteAllTextAsync(StepFile(i, "run.sh"), ExprEvaluator.Interpolate(step.Run, context), cancellationToken);
            var env = step.Env.ToDictionary(e => e.Key, e => ExprEvaluator.Interpolate(e.Value, context));
            env["GITHUB_OUTPUT"] = $"{Mount}/steps/{i}/output";

            Banner($"==> {step.Id}: {step.DisplayName} [{ImageOf(step)}]");
            var exitCode = await runStep(i, step, env, cancellationToken);
            var outputs = StepOutputFile.Parse(await File.ReadAllTextAsync(StepFile(i, "output"), cancellationToken));
            Finish(context, new StepResult(step.Id, exitCode == 0 ? StepOutcome.Success : StepOutcome.Failure, exitCode, outputs));
            if (exitCode != 0)
            {
                firstFailure ??= exitCode;
            }
        }

        return firstFailure ?? 0;
    }

    /// <summary>A fresh <c>docker run --rm</c> per step: nothing but the workspace survives from one step to the next.</summary>
    Task<int> RunStepAsync(int index, StepSpec step, IReadOnlyDictionary<string, string> env, CancellationToken cancellationToken) =>
        backend.RunContainerAsync(
            job,
            ContainerArgs(ImageOf(step), env, ["sh", "-e", $"{Mount}/steps/{index}/run.sh"]),
            output,
            gate,
            Started,
            cancellationToken);

    /// <summary>
    /// One long-lived container per image, started when a step first needs it, and <c>docker exec</c> per step:
    /// steps in the same image share its processes' view of the filesystem, <c>/tmp</c> included.
    /// </summary>
    async Task<int> ExecAsync(CancellationToken cancellationToken)
    {
        var containers = new Dictionary<string, string>();
        try
        {
            return await OrchestrateAsync(async (index, step, env, ct) =>
            {
                var image = ImageOf(step);
                if (!containers.TryGetValue(image, out var id))
                {
                    var run = await backend.Cli.RunAsync(
                        ["run", "--detach", "--rm", "--init", .. ContainerArgs(image, new Dictionary<string, string>(), ["tail", "-f", "/dev/null"])],
                        ct);
                    containers[image] = id = run.EnsureSuccess().Stdout.Trim();
                }

                Started();
                List<string> exec = ["exec", "--workdir", Workspace];
                foreach (var (name, value) in env)
                {
                    exec.AddRange(["--env", $"{name}={value}"]);
                }

                return await backend.StreamAsync([.. exec, id, "sh", "-e", $"{Mount}/steps/{index}/run.sh"], output, gate, ct);
            }, cancellationToken);
        }
        finally
        {
            // Also covers a container whose docker run was cancelled before it printed the ID.
            if (containers.Count == 0 || cancellationToken.IsCancellationRequested)
            {
                await backend.RemoveLeftoversAsync(null, job.JobId);
            }
            else
            {
                foreach (var id in containers.Values)
                {
                    await backend.RemoveLeftoversAsync(id, job.JobId);
                }
            }
        }
    }

    // ---- Orchestrated inside the containers ---------------------------------------------------------------------

    /// <summary>
    /// The steps' order, conditions, expressions and outputs are compiled to shell and handled inside the containers:
    /// one per image, all started at once, each running its own steps and waiting on
    /// <c>/octopus/steps/&lt;i&gt;/outcome</c> files for the rest. The runner only starts containers and reads results.
    /// </summary>
    async Task<int> InContainerAsync(CancellationToken cancellationToken)
    {
        var compiler = new ShellExprCompiler(Mount, job.Steps.Select(s => s.Id).ToArray());
        await File.WriteAllTextAsync(Path.Combine(state, "lib.sh"), compiler.Library, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(state, "driver.sh"), Driver, cancellationToken);
        for (var i = 0; i < job.Steps.Count; i++)
        {
            var step = job.Steps[i];
            await File.WriteAllTextAsync(StepFile(i, "step.sh"), StepScript(compiler, i, step), cancellationToken);
            var run = step.Run.Contains("${{", StringComparison.Ordinal) ? $". {Mount}/lib.sh\n{compiler.Script(step.Run)}" : step.Run;
            await File.WriteAllTextAsync(StepFile(i, "run.sh"), run, cancellationToken);
        }

        var groups = job.Steps
            .Select((step, index) => (Image: ImageOf(step), Index: index))
            .GroupBy(s => s.Image)
            .ToArray();

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = groups.ToDictionary(
            group => backend.RunContainerAsync(
                job,
                ContainerArgs(group.Key, new Dictionary<string, string>(), ["sh", $"{Mount}/driver.sh", .. group.Select(s => s.Index.ToString())]),
                output,
                gate,
                Started,
                stop.Token),
            group => group.Key);

        // A container that dies leaves the others waiting for its steps forever, so stop them all.
        Exception? failure = null;
        while (running.Count > 0)
        {
            var done = await Task.WhenAny(running.Keys);
            var image = running[done];
            running.Remove(done);
            try
            {
                var exitCode = await done;
                if (exitCode != 0)
                {
                    failure ??= new InvalidOperationException($"The {image} container exited {exitCode} before its steps finished.");
                }
            }
            catch (Exception e)
            {
                failure ??= e;
            }

            if (failure is not null)
            {
                await stop.CancelAsync();
            }
        }

        if (failure is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExceptionDispatchInfo.Throw(failure);
        }

        int? firstFailure = null;
        var context = new StepsContext();
        for (var i = 0; i < job.Steps.Count; i++)
        {
            var result = await ReadResultAsync(i, cancellationToken);
            Finish(context, result);
            if (result.Outcome == StepOutcome.Failure)
            {
                firstFailure ??= result.ExitCode ?? 1;
            }
        }

        return firstFailure ?? 0;
    }

    /// <summary>Runs the steps it's given, in order, each once the step before it (in any container) has an outcome.</summary>
    const string Driver = $$"""
        . {{Mount}}/lib.sh
        for i in "$@"; do
          if [ "$i" -gt 0 ]; then
            while [ ! -e "$__root/steps/$((i - 1))/outcome" ]; do sleep 0.05; done
          fi
          . "$__root/steps/$i/step.sh"
          if __cond; then
            echo "==> $__id: $__title [$__image]"
            ( __env && cd "$__root/workspace" && exec sh -e "$__root/steps/$i/run.sh" ) && rc=0 || rc=$?
            echo "$rc" > "$__root/steps/$i/exit_code"
            if [ "$rc" -eq 0 ]; then o=success; else o=failure; : > "$__root/failed"; fi
          else
            echo "==> $__id: $__title skipped"
            o=skipped
          fi
          echo "$o" > "$__root/steps/$i/outcome.tmp" && mv "$__root/steps/$i/outcome.tmp" "$__root/steps/$i/outcome"
        done
        exit 0

        """;

    string StepScript(ShellExprCompiler compiler, int index, StepSpec step)
    {
        var script = new StringBuilder();
        script.Append($"__id={Quote(step.Id)}\n__title={Quote(step.DisplayName)}\n__image={Quote(ImageOf(step))}\n");
        script.Append($"__cond() {{ {compiler.Condition(Expr.ParseCondition(step.If))}; }}\n");
        script.Append($"__env() {{\n  export GITHUB_OUTPUT=\"$__root/steps/{index}/output\"\n");
        foreach (var (name, value) in step.Env)
        {
            if (!EnvName().IsMatch(name))
            {
                throw new FormatException($"Step {step.Id} has an invalid environment variable name '{name}'.");
            }

            script.Append($"  export {name}={compiler.Word(value)}\n");
        }

        script.Append("}\n");
        return script.ToString();
    }

    async Task<StepResult> ReadResultAsync(int index, CancellationToken cancellationToken)
    {
        var step = job.Steps[index];
        var outcome = (await ReadOptionalAsync(StepFile(index, "outcome"), cancellationToken))?.Trim()
            ?? throw new InvalidOperationException($"Step {step.Id} never finished.");
        if (outcome == "skipped")
        {
            return StepResult.Skipped(step.Id);
        }

        var exitCode = int.Parse((await ReadOptionalAsync(StepFile(index, "exit_code"), cancellationToken) ?? "1").Trim());
        var outputs = StepOutputFile.Parse(await File.ReadAllTextAsync(StepFile(index, "output"), cancellationToken));
        return new StepResult(step.Id, outcome == "success" ? StepOutcome.Success : StepOutcome.Failure, exitCode, outputs);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvName();

    static string Quote(string value) => $"'{value.Replace("'", "'\\''")}'";

    static async Task<string?> ReadOptionalAsync(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;

    // ---- Shared ---------------------------------------------------------------------------------------------------

    string ImageOf(StepSpec step) => step.Image ?? options.JobImage;

    List<string> ContainerArgs(string image, IReadOnlyDictionary<string, string> env, IReadOnlyList<string> command)
    {
        List<string> args =
        [
            .. backend.HardeningArgs(job),
            "--tmpfs", "/tmp",
            "--volume", $"{state}:{Mount}",
            "--workdir", Workspace,
            "--env", "HOME=/tmp",
            "--env", $"GITHUB_WORKSPACE={Workspace}",
        ];
        foreach (var (name, value) in env)
        {
            args.AddRange(["--env", $"{name}={value}"]);
        }

        return [.. args, image, .. command];
    }

    void Started()
    {
        if (Interlocked.Exchange(ref started, 1) == 0)
        {
            output.Started(backend.Now);
        }
    }

    void Finish(StepsContext context, StepResult result)
    {
        context.Add(result);
        output.StepFinished(result);
    }

    void Banner(string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        lock (gate)
        {
            output.Write(bytes);
        }
    }

    string StepFile(int index, string name) => Path.Combine(state, "steps", index.ToString(), name);

    /// <summary>Creates the state directory, writable by the job's user, which isn't the runner's.</summary>
    string CreateStateDirectory()
    {
        var root = Path.Combine(Path.GetFullPath(options.StateRoot ?? Path.GetTempPath()), $"octopus-job-{job.JobId}");
        OpenDirectory(root);
        OpenDirectory(Path.Combine(root, "workspace"));
        OpenDirectory(Path.Combine(root, "steps"));
        for (var i = 0; i < job.Steps.Count; i++)
        {
            var dir = Path.Combine(root, "steps", i.ToString());
            OpenDirectory(dir);
            var outputFile = Path.Combine(dir, "output");
            File.WriteAllText(outputFile, "");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(outputFile, (UnixFileMode)0b110_110_110);
            }
        }

        return root;

        static void OpenDirectory(string path)
        {
            Directory.CreateDirectory(path);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, (UnixFileMode)0b111_111_111);
            }
        }
    }

    void DeleteStateDirectory()
    {
        if (options.KeepState || state.Length == 0)
        {
            return;
        }

        try
        {
            Directory.Delete(state, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Directories a step created in the workspace belong to the job's user, so the runner can't empty them.
            backend.Logger.LogWarning(e, "Couldn't delete the state directory {State} of job {JobId}", state, job.JobId);
        }
    }
}
