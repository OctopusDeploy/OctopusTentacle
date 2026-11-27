using System.Collections;
using System.Collections.Concurrent;
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
    const string StepRunnerImage = "octopus/pipelines-step-runner:test";

    [Theory]
    [InlineData("1-same-container.yml", new[] { "alpine:3.20", "alpine:3.20", "alpine:3.20" }, new[] { "2:alpine:3.20" })]
    [InlineData("2-different-middle-container.yml", new[] { "alpine:3.20", "python:3.12-alpine", "alpine:3.20" }, new[] { "1:python:3.12-alpine", "2:alpine:3.20" })]
    [InlineData("3-different-containers.yml", new[] { "debian:bookworm-slim", "python:3.12-alpine", "alpine:3.20" }, new[] { "0:debian:bookworm-slim", "1:python:3.12-alpine", "2:alpine:3.20" })]
    public async Task Runs_the_sample_workflows_and_releases_each_container_after_its_last_step(string file, string[] images, string[] releases)
    {
        // The in-process environment ignores images, so leave out the Python step's interpreter.
        var workflow = WorkflowFile.Load(Path.Combine(AppContext.BaseDirectory, "workflows", file));
        workflow = workflow with
        {
            Steps = workflow.Steps.Select(s => s.Id == "decide"
                ? s with { Run = "echo \"workspace has: $(cat release.txt)\"\necho deploy=true >> \"$OCTOPUS_OUTPUT\"\nprintf 'notes<<EOF\\nRelease %s\\nEOF\\n' \"$VERSION\" >> \"$OCTOPUS_OUTPUT\"", Shell = "sh" }
                : s with { Run = s.Run.Replace("$(grep -s ^PRETTY_NAME= /etc/os-release | cut -d'\"' -f2)", "host"), Shell = "sh" }).ToArray(),
        };
        var environment = new InProcessStepEnvironment();
        var output = new RecordingOutput();

        var exitCode = await Run(new JobSpec("j", "r", "") { Steps = workflow.ToSteps(StepRunnerImage, new Dictionary<string, string>()), Image = workflow.Image }, output, environment);

        Assert.Equal(0, exitCode);
        Assert.Equal(["generate:Success", "decide:Success", "deploy:Success"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}"));
        Assert.Equal("1.4.0", output.Steps[0].Outputs["version"]);
        Assert.Equal("Release 1.4.0", output.Steps[1].Outputs["notes"]);
        Assert.Contains("workspace has: built by generate", output.Text);
        Assert.Contains("deploying 1.4.0\nRelease 1.4.0\n", output.Text);
        Assert.Equal(images, environment.Executions.Select(e => e.Image));
        Assert.Equal(releases, environment.Releases);
        Assert.Equal(1, output.StartedCount);
    }

    [Fact]
    public async Task Passes_the_payload_through_untouched()
    {
        var environment = new InProcessStepEnvironment();
        var step = Step("a", "echo hi");

        await Run(Job(step), new RecordingOutput(), environment);

        Assert.Equal(step.Payload, environment.Payloads.Single());
    }

    [Fact]
    public async Task Passes_each_steps_outcome_and_outputs_to_the_steps_after_it()
    {
        var output = new RecordingOutput();
        var job = Job(
            Step("one", "echo \"greeting=hello\" >> \"$OCTOPUS_OUTPUT\"; exit 3"),
            Step("two", "echo never"),
            Step("three", "echo \"after ${{ steps.one.outcome }} with ${{ steps.one.outputs.greeting }}, two was ${{ steps.two.outcome }}\"", condition: "failure()"));

        var exitCode = await Run(job, output);

        Assert.Equal(3, exitCode);
        Assert.Equal(["one:Failure:3", "two:Skipped:", "three:Success:0"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}:{s.ExitCode}"));
        Assert.Contains("after failure with hello, two was skipped", output.Text);
    }

    [Fact]
    public async Task Stops_and_fails_a_step_that_runs_past_its_timeout()
    {
        var output = new RecordingOutput();
        var job = Job(Step("slow", "echo started; sleep 30") with { Timeout = TimeSpan.FromMilliseconds(500) }, Step("next", "echo never"));

        var exitCode = await Run(job, output);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(StepOutcome.Failure, output.Steps[0].Outcome);
        Assert.Equal("The step timed out after 00:00:00.5000000.", output.Steps[0].Error);
        Assert.Equal(StepOutcome.Skipped, output.Steps[1].Outcome);
        Assert.Contains("Error: The step timed out", output.Text);
    }

    [Fact]
    public async Task Stops_the_running_step_when_cancelled_and_runs_only_the_steps_that_ask_to()
    {
        var output = new RecordingOutput();
        var job = Job(
            Step("build", "echo started\nsleep 30"),
            Step("deploy", "echo never"),
            Step("cleanup", "echo \"cleaning up, job ${{ job.status }}, build ${{ steps.build.outcome }}\"", condition: "always()"));

        var run = Run(job, output);
        await WaitForAsync(() => output.Text.Contains("started"));
        job.Cancellation.Request();
        await run.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(["build:Cancelled", "deploy:Skipped", "cleanup:Success"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}"));
        Assert.Contains("cleaning up, job cancelled, build cancelled", output.Text);
    }

    [Fact]
    public async Task Stops_everything_at_once_when_a_cancellation_is_forced()
    {
        var output = new RecordingOutput();
        var job = Job(Step("build", "echo started\nsleep 30"), Step("cleanup", "echo cleaning up", condition: "always()"));

        var run = Run(job, output);
        await WaitForAsync(() => output.Text.Contains("started"));
        job.Cancellation.Request(force: true);
        await run.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(["build:Cancelled"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}"));
        Assert.DoesNotContain("cleaning up", output.Text);
    }

    [Fact]
    public async Task Cancels_the_job_when_it_runs_past_its_timeout()
    {
        var output = new RecordingOutput();
        var job = Job(Step("build", "echo started\nsleep 30"), Step("cleanup", "echo \"cleaning up, job ${{ job.status }}\"", condition: "always()")) with
        {
            Timeout = TimeSpan.FromMilliseconds(500),
        };

        await Run(job, output);

        Assert.True(job.Cancellation.IsRequested);
        Assert.Equal("The job timed out after 00:00:00.5000000.", job.Cancellation.Reason);
        Assert.Contains("cleaning up, job cancelled", output.Text);
    }

    [Fact]
    public async Task Fails_a_step_whose_step_runner_left_no_result_and_keeps_going()
    {
        var output = new RecordingOutput();
        var environment = new InProcessStepEnvironment { BrokenSteps = { "a" } };

        var exitCode = await Run(Job(Step("a", "echo hi"), Step("b", "echo \"a was ${{ steps.a.outcome }}\"", condition: "always()")), output, environment);

        Assert.Equal(1, exitCode);
        Assert.Equal("The step runner exited 7 without a result.", output.Steps[0].Error);
        Assert.Contains("a was failure", output.Text);
        // Something may still be running in its container, so the next step gets a new one.
        Assert.Equal("0:", environment.Releases[0]);
    }

    [Fact]
    public async Task Keeps_outputs_that_hold_a_secret_inside_the_job()
    {
        var output = new RecordingOutput();
        var secrets = new Dictionary<string, string> { ["TOKEN"] = "s3cr3t" };
        var job = Job(
            Step("login", "echo \"token=Bearer $TOKEN\" >> \"$OCTOPUS_OUTPUT\"\necho \"::add-mask::session-42\"\necho \"session=session-42\" >> \"$OCTOPUS_OUTPUT\"\necho user=me >> \"$OCTOPUS_OUTPUT\"", secrets: secrets, env: new() { ["TOKEN"] = "${{ secrets.TOKEN }}" }),
            Step("use", "echo \"got ${{ steps.login.outputs.token }} and ${{ steps.login.outputs.session }}\"", secrets: secrets));

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
    public async Task Refuses_a_job_with_two_steps_with_the_same_id() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(Job(Step("a", "true"), Step("A", "true")), new RecordingOutput()));

    static StepSpec Step(string id, string run, string? condition = null, Dictionary<string, string>? secrets = null, Dictionary<string, string>? env = null) =>
        new(id, StepRunnerImage, StepJsonContext.Write(new StepPayload
        {
            Id = id,
            If = condition,
            Shell = "sh",
            Run = run,
            Env = env ?? [],
            Secrets = secrets ?? [],
        }));

    static JobSpec Job(params StepSpec[] steps) => new("j", "r", "") { Steps = steps };

    static Task<int> Run(JobSpec job, RecordingOutput output, InProcessStepEnvironment? environment = null) =>
        new StepsOrchestrator(environment ?? new InProcessStepEnvironment()) { StopGracePeriod = TimeSpan.FromSeconds(5) }
            .RunAsync(job, output, TestContext.Current.CancellationToken);

    static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    /// <summary>
    /// Runs the step runner in process, with the containers' paths (<c>/octopus/...</c>) mapped to a temp directory.
    /// Every image shares this host, so it records which image each step ran in and when each was released.
    /// </summary>
    sealed class InProcessStepEnvironment : IStepEnvironment
    {
        public List<(string Image, int Step)> Executions { get; } = [];

        public List<string> Releases { get; } = [];

        public List<string> Payloads { get; } = [];

        /// <summary>Steps whose step runner exits 7 without a result.</summary>
        public HashSet<string> BrokenSteps { get; } = [];

        public Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "Runs scripts with sh.");
            var directory = Directory.CreateTempSubdirectory("steps-job-").FullName;
            SharedStorage.CreateJobDirectory(directory);
            return Task.FromResult<IStepSession>(new Session(this, directory, output));
        }

        sealed class Session(InProcessStepEnvironment owner, string directory, IJobOutput output) : IStepSession
        {
            int step = -1;

            public string Directory => directory;

            public async Task<int> ExecAsync(string image, IReadOnlyList<string> command, CancellationToken cancellationToken)
            {
                output.Started(DateTimeOffset.UtcNow);
                Assert.Equal("run", command[1]);
                var environment = new Hashtable { ["PATH"] = Environment.GetEnvironmentVariable("PATH") };
                foreach (var setting in command.Skip(2))
                {
                    var equals = setting.IndexOf('=');
                    environment[setting[..equals]] = setting[(equals + 1)..]
                        .Replace(StepLayout.WorkspacePath, Path.Combine(directory, StepLayout.WorkspaceDirectory))
                        .Replace(StepLayout.StepsPath, Path.Combine(directory, StepLayout.StepsDirectory))
                        .Replace(StepLayout.ResultsPath, Path.Combine(directory, StepLayout.ResultsDirectory));
                }

                var input = (string)environment[StepEnvironment.Input]!;
                owner.Executions.Add((image, ++step));
                owner.Payloads.Add(await File.ReadAllTextAsync(input, cancellationToken));
                if (owner.BrokenSteps.Contains(StepJsonContext.ReadPayload(owner.Payloads[^1]).Id))
                {
                    return 7;
                }

                return await new StepRunnerApp(environment, new OutputStream(output), TimeProvider.System).RunAsync(cancellationToken);
            }

            public Task ReleaseAsync(string image, CancellationToken cancellationToken)
            {
                owner.Releases.Add($"{step}:{image}");
                return Task.CompletedTask;
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
        readonly ConcurrentQueue<StepResult> steps = new();
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

        public IReadOnlyList<StepResult> Steps => steps.ToArray();

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

        public void StepFinished(StepResult result) => steps.Enqueue(result);
    }
}
