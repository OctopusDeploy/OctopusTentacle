using System.Collections;
using System.Text;
using Octopus.Pipelines.Runner.StepRunner;
using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Runs the step runner in process, with real scripts run by the host's sh.</summary>
public sealed class StepRunnerAppTests : IDisposable
{
    readonly string directory;

    public StepRunnerAppTests()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Runs scripts with sh.");
        directory = Directory.CreateTempSubdirectory("step-runner-").FullName;
        Directory.CreateDirectory(Path.Combine(directory, "workspace"));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    static readonly StepRunResult Decided = new()
    {
        Id = "decide",
        Outcome = StepRunOutcome.Success,
        ExitCode = 0,
        Outputs = new Dictionary<string, string> { ["deploy"] = "true" },
    };

    static StepPayload Payload(string id, string run) => new()
    {
        Id = id,
        Shell = "sh",
        Run = run,
        Env = new Dictionary<string, string> { ["RELEASE_VERSION"] = "1.4.0" },
        Secrets = new Dictionary<string, string> { ["TOKEN"] = "s3cr3t" },
    };

    string File_(string name) => Path.Combine(directory, name);

    async Task<(int ExitCode, StepRunResult? Result, string Log)> RunAsync(
        StepPayload payload,
        IEnumerable<StepRunResult>? earlier = null,
        string jobStatus = JobStatus.Success,
        CancellationTokenSource? cancel = null)
    {
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(File_("input.json"), StepJsonContext.Write(payload), token);
        await File.WriteAllTextAsync(File_("results.jsonl"), string.Concat((earlier ?? [Decided]).Select(r => StepJsonContext.Write(r) + "\n")), token);
        await File.WriteAllTextAsync(File_("output"), "", token);
        var environment = new Hashtable
        {
            [StepEnvironment.Input] = File_("input.json"),
            [StepEnvironment.Output] = File_("output"),
            [StepEnvironment.Result] = File_("result.json"),
            [StepEnvironment.Results] = File_("results.jsonl"),
            [StepEnvironment.Cancel] = File_("cancel"),
            [StepEnvironment.JobStatus] = jobStatus,
            [StepEnvironment.Workspace] = File_("workspace"),
            ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
        };
        using var log = new MemoryStream();
        var exitCode = await new StepRunnerApp(environment, log, TimeProvider.System).RunAsync(cancel?.Token ?? token);
        var result = File.Exists(File_("result.json")) ? StepJsonContext.ReadResult(await File.ReadAllTextAsync(File_("result.json"), token)) : null;
        return (exitCode, result, Encoding.UTF8.GetString(log.ToArray()));
    }

    [Fact]
    public async Task Runs_the_script_with_resolved_inputs_and_reports_its_outputs()
    {
        var payload = Payload("deploy", "echo \"in $(basename \"$PWD\") v=$VERSION inline=${{ steps.decide.outputs.deploy }}\"\necho \"version=$VERSION\" >> \"$OCTOPUS_OUTPUT\"\necho \"same=1\" >> \"$GITHUB_OUTPUT\"\necho \"input=${OCTOPUS_INPUT:-hidden} results=${OCTOPUS_RESULTS:-hidden}\"");
        payload.Name = "Deploy ${{ env.RELEASE_VERSION }}";
        payload.If = "steps.decide.outputs.deploy == 'true'";
        payload.Env["VERSION"] = "${{ env.RELEASE_VERSION }}";

        var (exitCode, result, log) = await RunAsync(payload);

        Assert.Equal(0, exitCode);
        Assert.Equal("deploy", result!.Id);
        Assert.Equal(StepRunOutcome.Success, result.Outcome);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new Dictionary<string, string> { ["version"] = "1.4.0", ["same"] = "1" }, result.Outputs);
        Assert.Empty(result.WithheldOutputs);
        Assert.Contains("==> deploy: Deploy 1.4.0\n", log);
        Assert.Contains("in workspace v=1.4.0 inline=true\n", log);
        Assert.Contains("input=hidden results=hidden\n", log);
    }

    [Fact]
    public async Task Writes_its_result_on_one_line()
    {
        await RunAsync(Payload("a", "echo hi"));

        Assert.DoesNotContain('\n', (await File.ReadAllTextAsync(File_("result.json"), TestContext.Current.CancellationToken)).Trim());
    }

    [Fact]
    public async Task Skips_a_step_whose_condition_is_false_without_running_it()
    {
        var payload = Payload("deploy", "echo ran");
        payload.If = "steps.decide.outputs.deploy == 'false'";

        var (exitCode, result, log) = await RunAsync(payload);

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Skipped, result!.Outcome);
        Assert.Null(result.ExitCode);
        Assert.Equal("==> deploy: deploy skipped\n", log);
    }

    [Fact]
    public async Task Skips_a_step_after_a_failed_step_unless_it_asks_to_run()
    {
        StepRunResult[] earlier = [Decided, new() { Id = "build", Outcome = StepRunOutcome.Failure, ExitCode = 2 }];
        var cleanup = Payload("b", "echo \"cleaning up after ${{ steps.build.outcome }}\"");
        cleanup.If = "failure()";

        var (_, skipped, _) = await RunAsync(Payload("a", "echo ran"), earlier);
        var (_, ran, log) = await RunAsync(cleanup, earlier);

        Assert.Equal(StepRunOutcome.Skipped, skipped!.Outcome);
        Assert.Equal(StepRunOutcome.Success, ran!.Outcome);
        Assert.Contains("cleaning up after failure", log);
    }

    [Fact]
    public async Task Runs_only_always_and_cancelled_steps_once_the_job_is_cancelled()
    {
        var always = Payload("a", "echo \"always ran, status ${{ job.status }}\"");
        always.If = "always()";
        var cancelled = Payload("c", "echo cancelled ran");
        cancelled.If = "cancelled()";

        var (_, plain, _) = await RunAsync(Payload("p", "echo plain ran"), jobStatus: JobStatus.Cancelled);
        var (_, ranAlways, alwaysLog) = await RunAsync(always, jobStatus: JobStatus.Cancelled);
        var (_, ranCancelled, _) = await RunAsync(cancelled, jobStatus: JobStatus.Cancelled);
        var (_, notCancelled, _) = await RunAsync(cancelled);

        Assert.Equal(StepRunOutcome.Skipped, plain!.Outcome);
        Assert.Equal(StepRunOutcome.Success, ranAlways!.Outcome);
        Assert.Contains("always ran, status cancelled", alwaysLog);
        Assert.Equal(StepRunOutcome.Success, ranCancelled!.Outcome);
        Assert.Equal(StepRunOutcome.Skipped, notCancelled!.Outcome);
    }

    [Fact]
    public async Task Reports_a_failing_script_with_its_exit_code_and_keeps_its_outputs()
    {
        var (exitCode, result, log) = await RunAsync(Payload("a", "echo \"partial=yes\" >> \"$OCTOPUS_OUTPUT\"\nexit 4"));

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Equal(4, result.ExitCode);
        Assert.Equal("yes", result.Outputs["partial"]);
        Assert.Contains("Process completed with exit code 4.", log);
    }

    [Fact]
    public async Task Masks_secrets_and_withholds_outputs_that_hold_them()
    {
        var payload = Payload("a", "echo \"token=$TOKEN\"\necho \"::add-mask::fetched-value\"\necho \"later fetched-value\"\necho \"header=Bearer $TOKEN\" >> \"$OCTOPUS_OUTPUT\"\necho \"session=fetched-value\" >> \"$OCTOPUS_OUTPUT\"\necho \"user=me\" >> \"$OCTOPUS_OUTPUT\"\nprintf 'no newline s3cr3t'");
        payload.Env["TOKEN"] = "${{ secrets.TOKEN }}";

        var (_, result, log) = await RunAsync(payload);

        Assert.Equal("==> a: a\ntoken=***\nlater ***\nno newline ***", log);
        Assert.Equal(["fetched-value"], result!.Masks);
        Assert.Equal(["header", "session"], result.WithheldOutputs.Order());
        Assert.Equal("Bearer s3cr3t", result.Outputs["header"]);
    }

    [Fact]
    public async Task Masks_values_earlier_steps_added()
    {
        StepRunResult[] earlier = [new() { Id = "login", Outcome = StepRunOutcome.Success, Masks = ["session-42"] }];

        var (_, _, log) = await RunAsync(Payload("a", "echo \"using session-42\""), earlier);

        Assert.Contains("using ***", log);
    }

    [Fact]
    public async Task Fails_the_step_on_an_invalid_condition_without_running_it()
    {
        var payload = Payload("a", "echo ran");
        payload.If = "steps.x ==";

        var (exitCode, result, log) = await RunAsync(payload);

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Null(result.ExitCode);
        Assert.StartsWith("The step's condition is invalid", result.Error);
        Assert.DoesNotContain("ran", log.Replace("Error", ""));
    }

    [Fact]
    public async Task Fails_the_step_when_its_shell_is_missing()
    {
        var payload = Payload("a", "echo hi");
        payload.Shell = "no-such-shell -c {0}";

        var (_, result, _) = await RunAsync(payload);

        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Contains("no-such-shell", result.Error);
    }

    [Fact]
    public async Task Fails_the_step_when_an_output_is_too_large()
    {
        var (_, result, _) = await RunAsync(Payload("a", "printf 'big=%01048577d\\n' 0 >> \"$OCTOPUS_OUTPUT\""));

        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Contains("larger than 1 MB", result.Error);
    }

    [Fact]
    public async Task Stops_the_script_when_the_cancel_file_appears_and_still_writes_a_result()
    {
        var run = RunAsync(Payload("a", "echo started\nsleep 30"));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(File_("cancel"), "", TestContext.Current.CancellationToken);

        var (exitCode, result, log) = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Cancelled, result!.Outcome);
        Assert.Equal("a", result.Id);
        Assert.Contains("The step was stopped.", log);
    }

    [Fact]
    public async Task Stops_the_script_when_signalled_and_still_writes_a_result()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        var (exitCode, result, _) = await RunAsync(Payload("a", "echo started\nsleep 30"), cancel: cts);

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Cancelled, result!.Outcome);
    }

    [Fact]
    public async Task Refuses_a_payload_version_it_does_not_know()
    {
        var payload = Payload("a", "echo hi");
        payload.Version = 99;

        var (exitCode, result, _) = await RunAsync(payload);

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Equal("The step's payload is version 99; this step runner reads version 1.", result.Error);
    }
}
