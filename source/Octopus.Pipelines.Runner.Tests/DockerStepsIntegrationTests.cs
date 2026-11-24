using System.Diagnostics;
using System.Text;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.Docker;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Runs jobs with steps in real containers, in every step mode; skipped where there is no Docker daemon.</summary>
public sealed class DockerStepsIntegrationTests
{
    const string OtherImage = "alpine:3.20";

    [Theory]
    [InlineData(DockerStepMode.Run)]
    [InlineData(DockerStepMode.Exec)]
    [InlineData(DockerStepMode.InContainer)]
    public async Task Passes_outputs_between_steps_and_skips_a_step_whose_condition_is_false(DockerStepMode mode)
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        using var backend = Backend(mode);
        var output = new RecordingOutput();
        var job = NewJob(
            new StepSpec("one", "echo \"greeting=hello\" >> \"$GITHUB_OUTPUT\""),
            new StepSpec("two", "echo \"loud=$(echo \"$GREETING\" | tr a-z A-Z)\" >> \"$GITHUB_OUTPUT\"; echo \"saw ${{ steps.one.outputs.greeting }}\"")
            {
                Image = OtherImage,
                Env = new Dictionary<string, string> { ["GREETING"] = "${{ steps.one.outputs.greeting }}" },
            },
            new StepSpec("three", "echo ran three") { If = "steps.two.outputs.loud == 'HELLO'" },
            new StepSpec("four", "echo ran four") { If = "steps.two.outputs.loud == 'nope'", Image = OtherImage });

        var exitCode = await backend.RunAsync(job, output, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(["one:Success", "two:Success", "three:Success", "four:Skipped"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}"));
        Assert.Equal("HELLO", output.Steps[1].Outputs["loud"]);
        Assert.Contains("saw hello", output.Text);
        Assert.Contains("ran three", output.Text);
        Assert.DoesNotContain("ran four", output.Text);
        Assert.Equal(0, CountContainers(job.JobId));
    }

    [Theory]
    [InlineData(DockerStepMode.Run)]
    [InlineData(DockerStepMode.Exec)]
    [InlineData(DockerStepMode.InContainer)]
    public async Task Fails_with_the_failed_steps_exit_code_and_runs_only_always_steps_after_it(DockerStepMode mode)
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        using var backend = Backend(mode);
        var output = new RecordingOutput();
        var job = NewJob(
            new StepSpec("one", "exit 3"),
            new StepSpec("two", "echo ran two"),
            new StepSpec("three", "echo cleaning up after ${{ steps.one.outcome }}") { If = "always()", Image = OtherImage });

        var exitCode = await backend.RunAsync(job, output, TestContext.Current.CancellationToken);

        Assert.Equal(3, exitCode);
        Assert.Equal(["one:Failure:3", "two:Skipped:", "three:Success:0"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}:{s.ExitCode}"));
        Assert.Contains("cleaning up after failure", output.Text);
        Assert.Equal(0, CountContainers(job.JobId));
    }

    [Theory]
    [InlineData(DockerStepMode.Run)]
    [InlineData(DockerStepMode.Exec)]
    [InlineData(DockerStepMode.InContainer)]
    public async Task Leaves_no_containers_or_state_behind_after_cancellation(DockerStepMode mode)
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        var stateRoot = Directory.CreateTempSubdirectory("steps-state-").FullName;
        using var backend = Backend(mode, stateRoot);
        var output = new RecordingOutput();
        var job = NewJob(new StepSpec("one", "echo started; sleep 60"), new StepSpec("two", "echo never") { Image = OtherImage });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = backend.RunAsync(job, output, cts.Token);
        for (var i = 0; i < 200 && !output.Text.Contains("started"); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.Contains("started", output.Text);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(0, CountContainers(job.JobId));
        Assert.Empty(Directory.GetFileSystemEntries(stateRoot));
    }

    static DockerJobBackend Backend(DockerStepMode mode, string? stateRoot = null) =>
        new(new DockerBackendOptions { StepMode = mode, StateRoot = stateRoot });

    static JobSpec NewJob(params StepSpec[] steps) => new($"it-{Guid.NewGuid():N}", "integration", "") { Steps = steps };

    static bool DockerAvailable()
    {
        try
        {
            return Docker("version", "--format", "{{.Server.Version}}").ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    static int CountContainers(string jobId) =>
        Docker("ps", "--all", "--quiet", "--filter", $"label=octopus.job-id={jobId}").Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    static (int ExitCode, string Stdout) Docker(params string[] args)
    {
        var startInfo = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout);
    }

    sealed class RecordingOutput : IJobOutput
    {
        readonly Lock gate = new();
        readonly MemoryStream bytes = new();
        readonly List<StepResult> steps = [];
        int started;

        public string Text
        {
            get
            {
                lock (gate)
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }
            }
        }

        public IReadOnlyList<StepResult> Steps => steps;

        public void Started(DateTimeOffset startedAt) => Assert.Equal(0, Interlocked.Exchange(ref started, 1));

        public void WaitingForNode() => throw new InvalidOperationException("Docker has no nodes.");

        public void Placed() => throw new InvalidOperationException("Docker has no nodes.");

        public void Write(ReadOnlySpan<byte> data)
        {
            lock (gate)
            {
                bytes.Write(data);
            }
        }

        public void StepFinished(StepResult result) => steps.Add(result);
    }
}
