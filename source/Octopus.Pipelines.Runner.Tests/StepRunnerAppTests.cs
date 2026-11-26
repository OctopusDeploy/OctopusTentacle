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

    static StepContext Context(string status = JobStatus.Success) => new()
    {
        Job = new StepContextJob { Status = status },
        Env = new Dictionary<string, string> { ["RELEASE_VERSION"] = "1.4.0" },
        Secrets = new Dictionary<string, string> { ["TOKEN"] = "s3cr3t" },
        Steps = new Dictionary<string, StepContextStep>
        {
            ["decide"] = new() { Outcome = StepRunOutcome.Success, Outputs = new Dictionary<string, string> { ["deploy"] = "true" } },
        },
    };

    async Task<(int ExitCode, StepRunResult? Result, string Log)> RunAsync(StepDefinition step, StepContext? context = null, CancellationTokenSource? cancel = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var input = new StepInput { Job = new StepJob { Id = "j", RunId = "r" }, Step = step, Context = context ?? Context() };
        await File.WriteAllTextAsync(Path.Combine(directory, "input.json"), StepJsonContext.Write(input), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "output"), "", cancellationToken);
        var environment = new Hashtable
        {
            [StepEnvironment.Input] = Path.Combine(directory, "input.json"),
            [StepEnvironment.Output] = Path.Combine(directory, "output"),
            [StepEnvironment.Result] = Path.Combine(directory, "result.json"),
            [StepEnvironment.Workspace] = Path.Combine(directory, "workspace"),
            ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
        };
        using var log = new MemoryStream();
        var exitCode = await new StepRunnerApp(environment, log, TimeProvider.System).RunAsync(cancel?.Token ?? cancellationToken);
        var resultPath = Path.Combine(directory, "result.json");
        var result = File.Exists(resultPath) ? StepJsonContext.ReadResult(await File.ReadAllTextAsync(resultPath, cancellationToken)) : null;
        return (exitCode, result, Encoding.UTF8.GetString(log.ToArray()));
    }

    [Fact]
    public async Task Runs_the_script_with_resolved_inputs_and_reports_its_outputs()
    {
        var (exitCode, result, log) = await RunAsync(new StepDefinition
        {
            Id = "deploy",
            Name = "Deploy ${{ env.RELEASE_VERSION }}",
            If = "steps.decide.outputs.deploy == 'true'",
            Shell = "sh",
            Env = new Dictionary<string, string> { ["VERSION"] = "${{ env.RELEASE_VERSION }}" },
            Run = "echo \"in $(basename \"$PWD\") v=$VERSION job=$RELEASE_VERSION inline=${{ steps.decide.outputs.deploy }}\"\necho \"version=$VERSION\" >> \"$OCTOPUS_OUTPUT\"\necho \"same=1\" >> \"$GITHUB_OUTPUT\"\necho \"input=${OCTOPUS_INPUT:-hidden}\"",
        });

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Success, result!.Outcome);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new Dictionary<string, string> { ["version"] = "1.4.0", ["same"] = "1" }, result.Outputs);
        Assert.Contains("==> deploy: Deploy 1.4.0\n", log);
        Assert.Contains("in workspace v=1.4.0 job=1.4.0 inline=true\n", log);
        Assert.Contains("input=hidden\n", log);
    }

    [Fact]
    public async Task Skips_a_step_whose_condition_is_false_without_running_it()
    {
        var (exitCode, result, log) = await RunAsync(new StepDefinition { Id = "deploy", If = "steps.decide.outputs.deploy == 'false'", Run = "echo ran" });

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Skipped, result!.Outcome);
        Assert.Null(result.ExitCode);
        Assert.Equal("==> deploy: deploy skipped\n", log);
    }

    [Fact]
    public async Task Skips_a_step_after_a_failure_unless_it_asks_to_run()
    {
        var (_, skipped, _) = await RunAsync(new StepDefinition { Id = "a", Run = "echo ran" }, Context(JobStatus.Failure));
        var (_, ran, log) = await RunAsync(new StepDefinition { Id = "b", If = "failure()", Run = "echo cleaning up" }, Context(JobStatus.Failure));

        Assert.Equal(StepRunOutcome.Skipped, skipped!.Outcome);
        Assert.Equal(StepRunOutcome.Success, ran!.Outcome);
        Assert.Contains("cleaning up", log);
    }

    [Fact]
    public async Task Reports_a_failing_script_with_its_exit_code_and_keeps_its_outputs()
    {
        var (exitCode, result, log) = await RunAsync(new StepDefinition { Id = "a", Run = "echo \"partial=yes\" >> \"$OCTOPUS_OUTPUT\"\nexit 4" });

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Equal(4, result.ExitCode);
        Assert.Equal("yes", result.Outputs["partial"]);
        Assert.Contains("Process completed with exit code 4.", log);
    }

    [Fact]
    public async Task Masks_secrets_and_values_added_with_add_mask()
    {
        var (_, result, log) = await RunAsync(new StepDefinition
        {
            Id = "a",
            Env = new Dictionary<string, string> { ["TOKEN"] = "${{ secrets.TOKEN }}" },
            Run = "echo \"token=$TOKEN\"\necho \"::add-mask::fetched-value\"\necho \"later fetched-value\"\nprintf 'no newline s3cr3t'",
        });

        Assert.Equal("==> a: a\ntoken=***\nlater ***\nno newline ***", log);
        Assert.Equal(["fetched-value"], result!.Masks);
    }

    [Fact]
    public async Task Fails_the_step_on_an_invalid_condition_without_running_it()
    {
        var (exitCode, result, log) = await RunAsync(new StepDefinition { Id = "a", If = "steps.x ==", Run = "echo ran" });

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Null(result.ExitCode);
        Assert.StartsWith("The step's condition is invalid", result.Error);
        Assert.DoesNotContain("ran", log.Replace("Error", ""));
    }

    [Fact]
    public async Task Fails_the_step_when_its_shell_is_missing()
    {
        var (_, result, _) = await RunAsync(new StepDefinition { Id = "a", Shell = "no-such-shell -c {0}", Run = "echo hi" });

        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Contains("no-such-shell", result.Error);
    }

    [Fact]
    public async Task Fails_the_step_when_an_output_is_too_large()
    {
        var (_, result, _) = await RunAsync(new StepDefinition { Id = "a", Run = "printf 'big=%01048577d\\n' 0 >> \"$OCTOPUS_OUTPUT\"" });

        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Contains("larger than 1 MB", result.Error);
    }

    [Fact]
    public async Task Stops_the_script_and_still_writes_a_result_when_cancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        var (exitCode, result, _) = await RunAsync(new StepDefinition { Id = "a", Run = "echo started\nsleep 30" }, cancel: cts);

        Assert.Equal(0, exitCode);
        Assert.Equal(StepRunOutcome.Failure, result!.Outcome);
        Assert.Equal("The step was cancelled.", result.Error);
    }

    [Fact]
    public async Task Refuses_an_input_version_it_does_not_know()
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "input.json"), "{\"version\": 99}", TestContext.Current.CancellationToken);
        var environment = new Hashtable
        {
            [StepEnvironment.Input] = Path.Combine(directory, "input.json"),
            [StepEnvironment.Result] = Path.Combine(directory, "result.json"),
        };

        var exitCode = await new StepRunnerApp(environment, Stream.Null, TimeProvider.System).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        var result = StepJsonContext.ReadResult(await File.ReadAllTextAsync(Path.Combine(directory, "result.json"), TestContext.Current.CancellationToken));
        Assert.Equal(StepRunOutcome.Failure, result.Outcome);
        Assert.Equal("The step input is version 99; this step runner reads version 1.", result.Error);
    }
}
