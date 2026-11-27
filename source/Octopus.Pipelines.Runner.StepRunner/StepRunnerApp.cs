using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.StepRunner;

/// <summary>
/// Runs one step: reads its payload and the earlier steps' results, decides from its condition whether it runs,
/// resolves its expressions, runs its script with masked output, and writes a <see cref="StepRunResult"/> to
/// <c>OCTOPUS_RESULT</c>. The job runner stops a step by creating the <c>OCTOPUS_CANCEL</c> file.
/// </summary>
/// <remarks>
/// Exits 0 whenever it wrote a result, whatever happened to the step, so the job runner can tell "the step failed"
/// (in the result) from "the step runner failed" (no result, or a non-zero exit).
/// </remarks>
sealed class StepRunnerApp(IDictionary environment, Stream stdout, TimeProvider clock)
{
    const int NoResultExitCode = 3;
    static readonly TimeSpan CancelPollInterval = TimeSpan.FromMilliseconds(200);

    readonly SecretMasker masker = new();
    readonly List<string> addedMasks = [];
    readonly Lock writeGate = new();
    string stepId = "";

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (Variable(StepEnvironment.Result) is not { } resultPath)
        {
            await Console.Error.WriteLineAsync($"{StepEnvironment.Result} isn't set: run the step runner as `octopus-step-runner run {StepEnvironment.Result}=<path> ...`.");
            return NoResultExitCode;
        }

        // The job runner's way to stop the step: it creates the cancel file. A signal does the same.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watching = WatchForCancelAsync(stop);

        var startedAt = clock.GetUtcNow();
        StepRunResult result;
        try
        {
            result = await RunStepAsync(stop.Token);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
        {
            WriteLine($"Error: {e.Message}");
            result = new StepRunResult { Outcome = StepRunOutcome.Failure, Error = e.Message };
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            result = Stopped(new StepRunResult());
        }
        finally
        {
            await stop.CancelAsync();
            await watching;
        }

        result.Id = stepId;
        result.StartedAt ??= startedAt;
        result.FinishedAt = clock.GetUtcNow();
        result.Masks = addedMasks;
        try
        {
            // Write, then rename: the job runner never sees half a result.
            var temporary = resultPath + ".tmp";
            await File.WriteAllTextAsync(temporary, StepJsonContext.Write(result), CancellationToken.None);
            File.Move(temporary, resultPath, overwrite: true);
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"Couldn't write the step's result to {resultPath}: {e.Message}");
            return NoResultExitCode;
        }
    }

    async Task WatchForCancelAsync(CancellationTokenSource stop)
    {
        if (Variable(StepEnvironment.Cancel) is not { } cancelPath)
        {
            return;
        }

        try
        {
            while (!stop.IsCancellationRequested)
            {
                if (File.Exists(cancelPath))
                {
                    await stop.CancelAsync();
                    return;
                }

                await Task.Delay(CancelPollInterval, clock, stop.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    async Task<StepRunResult> RunStepAsync(CancellationToken cancellationToken)
    {
        var inputPath = Variable(StepEnvironment.Input) ?? throw new InvalidDataException($"{StepEnvironment.Input} isn't set.");
        var payload = StepJsonContext.ReadPayload(await File.ReadAllTextAsync(inputPath, cancellationToken));
        stepId = payload.Id;
        if (payload.Version != StepPayload.CurrentVersion)
        {
            throw new InvalidDataException($"The step's payload is version {payload.Version}; this step runner reads version {StepPayload.CurrentVersion}.");
        }

        var earlier = Variable(StepEnvironment.Results) is { } resultsPath && File.Exists(resultsPath)
            ? StepJsonContext.ReadResults(await File.ReadAllTextAsync(resultsPath, cancellationToken))
            : [];
        var stepContext = StepContext.For(payload, earlier, Variable(StepEnvironment.JobStatus) ?? JobStatus.Success);
        foreach (var secret in stepContext.Secrets.Values.Concat(stepContext.Masks))
        {
            masker.Add(secret);
        }

        var failed = new StepRunResult { Outcome = StepRunOutcome.Failure };
        var context = new ExpressionContext(stepContext);
        string title;
        bool runs;
        try
        {
            title = payload.Name is { } name ? ExprEvaluator.Interpolate(name, context) : payload.Id;
            runs = ExprEvaluator.IsTrue(Expr.ParseCondition(payload.If), context);
        }
        catch (Exception e) when (e is FormatException or NotSupportedException)
        {
            return Fail(failed, $"The step's condition is invalid: {e.Message}");
        }

        if (!runs)
        {
            WriteLine($"==> {payload.Id}: {title} skipped");
            return new StepRunResult { Outcome = StepRunOutcome.Skipped };
        }

        WriteLine($"==> {payload.Id}: {title}");
        Dictionary<string, string> env;
        string script;
        Shell shell;
        try
        {
            env = payload.Env.ToDictionary(e => e.Key, e => ExprEvaluator.Interpolate(e.Value, context));
            // As GitHub Actions does: the value lands in the script verbatim. Pass untrusted values through env.
            script = ExprEvaluator.Interpolate(payload.Run, context);
            shell = Shell.Resolve(payload.Shell, Variable("PATH"));
        }
        catch (Exception e) when (e is FormatException or NotSupportedException)
        {
            return Fail(failed, e.Message);
        }

        var outputPath = Variable(StepEnvironment.Output);
        var workspace = Variable(StepEnvironment.Workspace);
        var scriptPath = Path.Combine(Path.GetTempPath(), $"octopus-step-{Guid.NewGuid():N}{shell.Extension}");
        await File.WriteAllTextAsync(scriptPath, script, cancellationToken);
        try
        {
            var startInfo = shell.StartInfo(scriptPath);
            startInfo.WorkingDirectory = workspace ?? Environment.CurrentDirectory;
            Prepare(startInfo.Environment, env, outputPath, workspace);

            int exitCode;
            try
            {
                exitCode = await RunScriptAsync(startInfo, cancellationToken);
            }
            catch (Win32Exception e)
            {
                return Fail(failed, $"Couldn't run '{startInfo.FileName}': {e.Message}. The step's image needs it.");
            }

            var result = new StepRunResult
            {
                Outcome = exitCode == 0 ? StepRunOutcome.Success : StepRunOutcome.Failure,
                ExitCode = exitCode,
            };
            if (cancellationToken.IsCancellationRequested)
            {
                Stopped(result);
            }
            else if (exitCode != 0)
            {
                WriteLine($"Process completed with exit code {exitCode}.");
            }

            return ReadOutputs(result, outputPath);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    StepRunResult Stopped(StepRunResult result)
    {
        WriteLine("The step was stopped.");
        result.Outcome = StepRunOutcome.Cancelled;
        result.Error = "The step was stopped before it finished.";
        return result;
    }

    /// <summary>
    /// The script's environment: the container's, then the step's, then the step files. The step runner's own
    /// variables (the payload, results and result paths) aren't passed on: the payload holds every secret.
    /// </summary>
    static void Prepare(IDictionary<string, string?> target, Dictionary<string, string> step, string? outputPath, string? workspace)
    {
        foreach (var name in new[] { StepEnvironment.Input, StepEnvironment.Result, StepEnvironment.Results, StepEnvironment.Cancel, StepEnvironment.JobStatus })
        {
            target.Remove(name);
        }

        foreach (var (name, value) in step)
        {
            target[name] = value;
        }

        // Set after the step's env, so a step can't point them somewhere else.
        if (outputPath is not null)
        {
            target[StepEnvironment.Output] = outputPath;
            target[StepEnvironment.GitHubOutput] = outputPath;
        }

        if (workspace is not null)
        {
            target[StepEnvironment.Workspace] = workspace;
            target[StepEnvironment.GitHubWorkspace] = workspace;
        }
    }

    async Task<int> RunScriptAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;

        using var process = Process.Start(startInfo) ?? throw new Win32Exception($"Couldn't start {startInfo.FileName}.");
        process.StandardInput.Close();
        await using var kill = cancellationToken.Register(static state => Kill((Process)state!), process);

        // stdout and stderr are masked line by line, so a secret is never split across two writes.
        await Task.WhenAll(
            PumpAsync(process.StandardOutput.BaseStream),
            PumpAsync(process.StandardError.BaseStream));
        await process.WaitForExitAsync(CancellationToken.None);
        return process.ExitCode;
    }

    async Task PumpAsync(Stream source)
    {
        const int MaxLine = 64 * 1024;
        var buffer = new byte[8192];
        var line = new MemoryStream();
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            var data = buffer.AsMemory(0, read);
            while (data.Length > 0)
            {
                var newline = data.Span.IndexOf((byte)'\n');
                if (newline < 0)
                {
                    line.Write(data.Span);
                    if (line.Length >= MaxLine)
                    {
                        // A line this long is written in pieces; a secret straddling a piece boundary isn't masked.
                        EmitLine(line, newline: false);
                    }

                    break;
                }

                line.Write(data.Span[..newline]);
                EmitLine(line, newline: true);
                data = data[(newline + 1)..];
            }
        }

        if (line.Length > 0)
        {
            EmitLine(line, newline: false);
        }
    }

    void EmitLine(MemoryStream line, bool newline)
    {
        var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length);
        line.SetLength(0);
        var trimmed = text.TrimEnd('\r');
        if (trimmed.StartsWith(SecretMasker.AddMaskCommand, StringComparison.Ordinal))
        {
            var value = trimmed[SecretMasker.AddMaskCommand.Length..];
            if (value.Length > 0)
            {
                masker.Add(value);
                lock (addedMasks)
                {
                    addedMasks.Add(value);
                }
            }

            return;
        }

        Write(masker.Apply(text) + (newline ? "\n" : ""));
    }

    StepRunResult ReadOutputs(StepRunResult result, string? outputPath)
    {
        if (outputPath is null || !File.Exists(outputPath))
        {
            return result;
        }

        try
        {
            result.Outputs = StepOutputFile.Parse(File.ReadAllText(outputPath)).ToDictionary();
        }
        catch (FormatException e)
        {
            return Fail(result, $"The step's outputs are invalid: {e.Message}");
        }

        if (result.Outputs.FirstOrDefault(o => Encoding.UTF8.GetByteCount(o.Value) > StepEnvironment.MaxOutputBytes) is { Key: { } tooBig })
        {
            return Fail(result, $"Output '{tooBig}' is larger than {StepEnvironment.MaxOutputBytes / 1024 / 1024} MB.");
        }

        // As GitHub Actions does for job outputs: later steps can use these, but they don't leave the job.
        result.WithheldOutputs = result.Outputs.Where(o => masker.Contains(o.Value)).Select(o => o.Key).ToList();
        return result;
    }

    StepRunResult Fail(StepRunResult result, string error)
    {
        WriteLine($"Error: {error}");
        result.Outcome = StepRunOutcome.Failure;
        result.Error = error;
        return result;
    }

    void WriteLine(string line) => Write(masker.Apply(line) + "\n");

    void Write(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        lock (writeGate)
        {
            stdout.Write(bytes);
            stdout.Flush();
        }
    }

    string? Variable(string name) => environment[name] is string { Length: > 0 } value ? value : null;

    static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }
}
