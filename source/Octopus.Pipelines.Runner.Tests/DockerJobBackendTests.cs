using System.Text;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Docker;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class DockerJobBackendTests
{
    static readonly JobSpec Job = new("job-1", "run-1", "echo hi");

    static DockerJobBackend CreateBackend(FakeDocker docker) => new(new DockerBackendOptions { Executable = docker.Path }) { DrainTimeout = TimeSpan.FromMilliseconds(500) };

    [Fact]
    public async Task Refuses_sandboxed_jobs()
    {
        using var docker = FakeDocker.Create();
        using var backend = CreateBackend(docker);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            backend.RunAsync(Job with { Isolation = JobIsolation.Sandboxed }, new RecordingOutput(), TestContext.Current.CancellationToken));

        Assert.Empty(docker.Commands);
    }

    [Fact]
    public async Task Runs_a_locked_down_container_that_removes_itself()
    {
        using var docker = FakeDocker.Create();
        using var backend = CreateBackend(docker);

        await backend.RunAsync(Job, new RecordingOutput(), TestContext.Current.CancellationToken);

        var args = docker.RunArgs;
        Assert.Equal(["run", "--rm", "--init", "--cidfile"], args.Take(4));
        Assert.EndsWith(".cid", args[4]);
        Assert.Equal(
            [
                "--pull", "never", "--network", "none", "--user", "65534:65534",
                "--memory", "33554432", "--cpus", "0.25", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
                "--label", "octopus.job-id=job-1", "--label", "octopus.run-id=run-1", "--read-only",
                "busybox:1.37", "sh", "-c", "echo hi",
            ],
            args.Skip(5));
        Assert.False(File.Exists(args[4]), "the cidfile is deleted after the run");
    }

    [Fact]
    public async Task Needs_no_cleanup_when_the_run_exits_by_itself()
    {
        using var docker = FakeDocker.Create();
        using var backend = CreateBackend(docker);

        await backend.RunAsync(Job, new RecordingOutput(), TestContext.Current.CancellationToken);

        Assert.Equal(["image", "run"], docker.Verbs);
    }

    [Fact]
    public async Task Pulls_a_missing_image_before_running()
    {
        using var docker = FakeDocker.Create(imagePresent: false);
        using var backend = CreateBackend(docker);

        await backend.RunAsync(Job, new RecordingOutput(), TestContext.Current.CancellationToken);

        Assert.Equal(["image", "pull", "run"], docker.Verbs);
        Assert.Contains("pull --quiet busybox:1.37", docker.Commands);
    }

    [Fact]
    public async Task Streams_stdout_and_stderr_to_the_output()
    {
        using var docker = FakeDocker.Create(run: "printf 'out1\\n'; sleep 0.2; printf 'err1\\n' >&2; sleep 0.2; printf 'out2\\n'");
        using var backend = CreateBackend(docker);
        var output = new RecordingOutput();

        await backend.RunAsync(Job, output, TestContext.Current.CancellationToken);

        Assert.Equal("out1\nerr1\nout2\n", output.Text);
        Assert.NotNull(output.StartedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(126)]
    [InlineData(127)]
    public async Task Returns_the_jobs_exit_code(int exitCode)
    {
        using var docker = FakeDocker.Create(run: $"exit {exitCode}");
        using var backend = CreateBackend(docker);

        Assert.Equal(exitCode, await backend.RunAsync(Job, new RecordingOutput(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reports_exit_code_125_as_a_docker_failure()
    {
        using var docker = FakeDocker.Create(run: """
            echo 'docker: Error response from daemon: unable to find user nobody2' >&2
            echo '' >&2
            echo "Run 'docker run --help' for more information" >&2
            exit 125
            """);
        using var backend = CreateBackend(docker);
        var output = new RecordingOutput();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => backend.RunAsync(Job, output, TestContext.Current.CancellationToken));

        Assert.EndsWith("Error response from daemon: unable to find user nobody2", error.Message);
        Assert.Contains("unable to find user nobody2", output.Text);
        Assert.Equal(["image", "run"], docker.Verbs);
    }

    [Fact]
    public async Task Stops_the_container_when_cancelled()
    {
        using var docker = FakeDocker.Create(run: "write_cid c1; echo $$ > \"$dir/run.pid\"; exec sleep 30");
        using var backend = CreateBackend(docker);
        var output = new RecordingOutput();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = backend.RunAsync(Job, output, cts.Token);
        await docker.WhenRunStartedAsync();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        // The fake's stop ends the run like Docker's would, and --rm removes the container: nothing else to do.
        Assert.Equal("stop --time 5 c1", docker.Commands.Last());
    }

    [Fact]
    public async Task Removes_the_container_when_the_run_does_not_exit_after_stop()
    {
        using var docker = FakeDocker.Create(run: "write_cid c1; echo $$ > \"$dir/run.pid\"; exec sleep 30", stop: "exit 0");
        using var backend = CreateBackend(docker);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = backend.RunAsync(Job, new RecordingOutput(), cts.Token);
        await docker.WhenRunStartedAsync();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        Assert.Equal(["stop --time 5 c1", "rm --force c1"], docker.Commands.TakeLast(2));
    }

    [Fact]
    public async Task Removes_leftovers_by_label_when_cancelled_before_the_container_has_an_id()
    {
        using var docker = FakeDocker.Create(run: "echo $$ > \"$dir/run.pid\"; exec sleep 30", ps: "echo c9");
        using var backend = CreateBackend(docker);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = backend.RunAsync(Job, new RecordingOutput(), cts.Token);
        await docker.WhenRunStartedAsync();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(docker.Verbs, v => v == "stop");
        Assert.Equal(["ps --all --quiet --filter label=octopus.job-id=job-1", "rm --force c9"], docker.Commands.TakeLast(2));
    }

    [Fact]
    public async Task Keeps_the_cancellation_when_removing_the_container_fails()
    {
        using var docker = FakeDocker.Create(run: "write_cid c1; echo $$ > \"$dir/run.pid\"; exec sleep 30", stop: "exit 0", rm: "echo 'Error response from daemon: removal already in progress' >&2; exit 1");
        using var backend = CreateBackend(docker);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = backend.RunAsync(Job, new RecordingOutput(), cts.Token);
        await docker.WhenRunStartedAsync();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        Assert.Equal("rm", docker.Verbs.Last());
    }

    [Fact]
    public async Task Names_the_cli_when_it_is_missing()
    {
        using var backend = new DockerJobBackend(new DockerBackendOptions { Executable = "/nonexistent/docker" });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => backend.RunAsync(Job, new RecordingOutput(), TestContext.Current.CancellationToken));

        Assert.Contains("/nonexistent/docker", error.Message);
        Assert.Contains("PATH", error.Message);
    }

    /// <summary>A shell script standing in for the docker CLI. It records every command it's given.</summary>
    sealed class FakeDocker : IDisposable
    {
        readonly string directory;

        FakeDocker(string directory)
        {
            this.directory = directory;
        }

        public string Path => System.IO.Path.Combine(directory, "docker");

        /// <summary>Each invocation's arguments joined by spaces, in order.</summary>
        public IReadOnlyList<string> Commands => ReadLines("commands");

        public IEnumerable<string> Verbs => Commands.Select(c => c.Split(' ')[0]);

        /// <summary>The arguments of the last <c>docker run</c>, one per element.</summary>
        public IReadOnlyList<string> RunArgs => ReadLines("run-args");

        public static FakeDocker Create(
            bool imagePresent = true,
            string run = "printf 'hello\\n'",
            string stop = "kill \"$(cat \"$dir/run.pid\")\"",
            string rm = "exit 0",
            string ps = "true")
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake docker CLI is a POSIX shell script.");
            var directory = Directory.CreateTempSubdirectory("fake-docker-").FullName;
            var script = $$"""
                #!/bin/sh
                dir='{{directory}}'
                echo "$*" >> "$dir/commands"
                # Writes the container ID to the --cidfile path, as docker run does once it has created the container.
                write_cid() {
                  printf '%s\n' "$1" > "$(awk 'p { print; exit } $0 == "--cidfile" { p = 1 }' "$dir/run-args")"
                }
                case "$1" in
                  image) {{(imagePresent ? "echo sha256:1" : "echo 'Error: No such image: busybox:1.37' >&2; exit 1")}} ;;
                  pull) exit 0 ;;
                  run)
                    for a in "$@"; do printf '%s\n' "$a"; done > "$dir/run-args"
                    {{run}}
                    ;;
                  stop) {{stop}} ;;
                  rm) {{rm}} ;;
                  ps) {{ps}} ;;
                  *) echo "unexpected: $*" >&2; exit 99 ;;
                esac
                """;
            var path = System.IO.Path.Combine(directory, "docker");
            File.WriteAllText(path, script.ReplaceLineEndings("\n"));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            return new FakeDocker(directory);
        }

        /// <summary>Waits until the fake docker run has written its PID, i.e. it's running the "container".</summary>
        public async Task WhenRunStartedAsync()
        {
            var pid = System.IO.Path.Combine(directory, "run.pid");
            for (var i = 0; i < 200 && !(File.Exists(pid) && new FileInfo(pid).Length > 0); i++)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(File.Exists(pid), "the fake docker run never started");
        }

        public void Dispose() => Directory.Delete(directory, recursive: true);

        IReadOnlyList<string> ReadLines(string name)
        {
            var path = System.IO.Path.Combine(directory, name);
            return File.Exists(path) ? File.ReadAllLines(path) : [];
        }
    }

    sealed class RecordingOutput : IJobOutput
    {
        readonly MemoryStream bytes = new();
        readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Text
        {
            get
            {
                lock (bytes)
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }
            }
        }

        public DateTimeOffset? StartedAt { get; private set; }

        public Task WhenStarted => started.Task;

        public void Started(DateTimeOffset startedAt)
        {
            StartedAt = startedAt;
            started.TrySetResult();
        }

        public void WaitingForNode() => throw new InvalidOperationException("Docker has no nodes.");

        public void Placed() => throw new InvalidOperationException("Docker has no nodes.");

        public void Write(ReadOnlySpan<byte> data)
        {
            lock (bytes)
            {
                bytes.Write(data);
            }
        }
    }
}
