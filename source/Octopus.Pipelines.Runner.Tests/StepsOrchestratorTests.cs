using System.Collections;
using System.Text;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.StepRunner;
using Octopus.Pipelines.Runner.Steps;
using Octopus.Pipelines.Runner.Workflows;
using StepResult = Octopus.Pipelines.Runner.Core.Steps.StepResult;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>The job runner's orchestration with the real step runner, in process: no containers, scripts run by sh.</summary>
public sealed class StepsOrchestratorTests
{
    [Theory]
    [InlineData("1-same-container.yml")]
    [InlineData("2-different-middle-container.yml")]
    [InlineData("3-different-containers.yml")]
    public async Task Runs_the_sample_workflows(string file)
    {
        // The in-process environment ignores images, so leave out the Python step's interpreter.
        var workflow = WorkflowFile.Load(Path.Combine(AppContext.BaseDirectory, "workflows", file));
        var steps = workflow.Steps.Select(s => s.Id == "decide"
            ? s with { Run = "echo \"workspace has: $(cat release.txt)\"\necho deploy=true >> \"$OCTOPUS_OUTPUT\"\nprintf 'notes<<EOF\\nRelease %s\\nEOF\\n' \"$VERSION\" >> \"$OCTOPUS_OUTPUT\"" }
            : s with { Run = s.Run.Replace("$(grep -s ^PRETTY_NAME= /etc/os-release | cut -d'\"' -f2)", "host") });
        var output = new RecordingOutput();

        var exitCode = await Run(new JobSpec("j", "r", "") { Steps = steps.ToArray(), Env = workflow.Env }, output);

        Assert.Equal(0, exitCode);
        Assert.Equal(["generate:Success", "decide:Success", "deploy:Success"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}"));
        Assert.Equal("1.4.0", output.Steps[0].Outputs["version"]);
        Assert.Equal("Release 1.4.0", output.Steps[1].Outputs["notes"]);
        Assert.Contains("workspace has: built by generate", output.Text);
        Assert.Contains("deploying 1.4.0\nRelease 1.4.0\n", output.Text);
        Assert.Equal(1, output.StartedCount);
    }

    [Fact]
    public async Task Passes_each_steps_outcome_and_outputs_to_the_steps_after_it()
    {
        var output = new RecordingOutput();
        var job = Job(
            new StepSpec("one", "echo \"greeting=hello\" >> \"$OCTOPUS_OUTPUT\"; exit 3"),
            new StepSpec("two", "echo never"),
            new StepSpec("three", "echo \"after ${{ steps.one.outcome }} with ${{ steps.one.outputs.greeting }}, two was ${{ steps.two.outcome }}\"") { If = "failure()" });

        var exitCode = await Run(job, output);

        Assert.Equal(3, exitCode);
        Assert.Equal(["one:Failure:3", "two:Skipped:", "three:Success:0"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}:{s.ExitCode}"));
        Assert.Contains("after failure with hello, two was skipped", output.Text);
    }

    [Fact]
    public async Task Stops_and_fails_a_step_that_runs_past_its_timeout()
    {
        var output = new RecordingOutput();
        var job = Job(
            new StepSpec("slow", "echo started; sleep 30") { Timeout = TimeSpan.FromMilliseconds(500) },
            new StepSpec("next", "echo never"));

        var exitCode = await Run(job, output);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(StepOutcome.Failure, output.Steps[0].Outcome);
        Assert.Equal("The step timed out after 00:00:00.5000000.", output.Steps[0].Error);
        Assert.Equal(StepOutcome.Skipped, output.Steps[1].Outcome);
        Assert.Contains("Error: The step timed out", output.Text);
    }

    [Fact]
    public async Task Fails_a_step_whose_step_runner_left_no_result()
    {
        var output = new RecordingOutput();

        var exitCode = await new StepsOrchestrator(new InProcessStepEnvironment(brokenExitCode: 7)).RunAsync(Job(new StepSpec("a", "echo hi")), output, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal("The step runner exited 7 without a result.", output.Steps.Single().Error);
    }

    [Fact]
    public async Task Keeps_outputs_that_hold_a_secret_inside_the_job()
    {
        var output = new RecordingOutput();
        var job = Job(
            new StepSpec("login", "echo \"token=Bearer $TOKEN\" >> \"$OCTOPUS_OUTPUT\"\necho \"::add-mask::session-42\"\necho \"session=session-42\" >> \"$OCTOPUS_OUTPUT\"\necho user=me >> \"$OCTOPUS_OUTPUT\"")
            {
                Env = new Dictionary<string, string> { ["TOKEN"] = "${{ secrets.TOKEN }}" },
            },
            new StepSpec("use", "echo \"got ${{ steps.login.outputs.token }} and ${{ steps.login.outputs.session }}\""))
            with
            {
                Secrets = new Dictionary<string, string> { ["TOKEN"] = "s3cr3t" },
            };

        await Run(job, output);

        Assert.Equal(new Dictionary<string, string> { ["user"] = "me" }, output.Steps[0].Outputs);
        Assert.Contains("Skipped output 'token' of step 'login' since it may contain a secret.", output.Text);
        Assert.Contains("Skipped output 'session' of step 'login' since it may contain a secret.", output.Text);
        // The next step still gets both, and its log masks them, including the mask the first step added.
        Assert.Contains("got Bearer *** and ***", output.Text);
        Assert.DoesNotContain("s3cr3t", output.Text);
        Assert.DoesNotContain("session-42", output.Text);
    }

    [Fact]
    public async Task Refuses_a_job_with_two_steps_with_the_same_id()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(Job(new StepSpec("a", "true"), new StepSpec("A", "true")), new RecordingOutput()));
    }

    static JobSpec Job(params StepSpec[] steps) => new("j", "r", "") { Steps = steps.Select(s => s with { Shell = s.Shell ?? "sh" }).ToArray() };

    static Task<int> Run(JobSpec job, RecordingOutput output) =>
        new StepsOrchestrator(new InProcessStepEnvironment()).RunAsync(job with { Steps = job.Steps.Select(s => s with { Shell = s.Shell ?? "sh" }).ToArray() }, output, TestContext.Current.CancellationToken);

    /// <summary>Runs the step runner in process, with the container's <c>/octopus</c> paths mapped to a temp directory.</summary>
    sealed class InProcessStepEnvironment(int? brokenExitCode = null) : IStepEnvironment
    {
        public Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "Runs scripts with sh.");
            var directory = Directory.CreateTempSubdirectory("steps-job-").FullName;
            Directory.CreateDirectory(Path.Combine(directory, StepLayout.WorkspaceDirectory));
            return Task.FromResult<IStepSession>(new Session(directory, output, brokenExitCode));
        }

        sealed class Session(string directory, IJobOutput output, int? brokenExitCode) : IStepSession
        {
            public string Directory => directory;

            public async Task<int> RunStepAsync(int index, StepSpec step, CancellationToken cancellationToken)
            {
                output.Started(DateTimeOffset.UtcNow);
                if (brokenExitCode is { } broken)
                {
                    return broken;
                }

                var environment = new Hashtable { ["PATH"] = Environment.GetEnvironmentVariable("PATH") };
                foreach (var (name, value) in StepLayout.Variables(index))
                {
                    environment[name] = directory + value[StepLayout.MountPath.Length..];
                }

                return await new StepRunnerApp(environment, new OutputStream(output), TimeProvider.System).RunAsync(cancellationToken);
            }

            public ValueTask DisposeAsync()
            {
                System.IO.Directory.Delete(directory, recursive: true);
                return ValueTask.CompletedTask;
            }
        }

        sealed class OutputStream(IJobOutput output) : Stream
        {
            public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer.AsSpan(offset, count));
            public override void Flush() { }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }

    internal sealed class RecordingOutput : IJobOutput
    {
        readonly Lock gate = new();
        readonly MemoryStream bytes = new();
        readonly List<StepResult> steps = [];
        int startedCount;

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

        public int StartedCount => startedCount;

        public void Started(DateTimeOffset startedAt) => Interlocked.Increment(ref startedCount);

        public void WaitingForNode()
        {
        }

        public void Placed()
        {
        }

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
