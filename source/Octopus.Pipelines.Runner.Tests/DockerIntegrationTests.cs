using System.Diagnostics;
using System.Text;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Docker;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Runs real containers through the docker CLI; skipped where there is no CLI or no daemon.</summary>
public sealed class DockerIntegrationTests
{
    [Fact]
    public async Task Runs_a_command_and_returns_its_exit_code()
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        using var backend = new DockerJobBackend(new DockerBackendOptions());
        var output = new RecordingOutput();

        var exitCode = await backend.RunAsync(NewJob("echo hello; exit 3"), output, TestContext.Current.CancellationToken);

        Assert.Equal(3, exitCode);
        Assert.Equal("hello\n", output.Text);
        Assert.NotNull(output.StartedAt);
    }

    [Fact]
    public async Task Has_no_network()
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        using var backend = new DockerJobBackend(new DockerBackendOptions());
        var output = new RecordingOutput();

        await backend.RunAsync(NewJob("wget -T 2 -q -O- http://1.1.1.1 || echo offline"), output, TestContext.Current.CancellationToken);

        Assert.Contains("offline", output.Text);
    }

    [Fact]
    public async Task Leaves_no_container_behind_after_cancellation()
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        using var backend = new DockerJobBackend(new DockerBackendOptions());
        var output = new RecordingOutput();
        var job = NewJob("sleep 60");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = backend.RunAsync(job, output, cts.Token);
        await output.WhenStarted.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        // Started fires when docker run launches; the container appears a moment later.
        for (var i = 0; i < 100 && CountContainers(job.JobId) == 0; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, CountContainers(job.JobId));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(0, CountContainers(job.JobId));
    }

    [Fact]
    public async Task Leaves_no_container_behind_after_a_job_finishes()
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        using var backend = new DockerJobBackend(new DockerBackendOptions());
        var job = NewJob("echo done");

        await backend.RunAsync(job, new RecordingOutput(), TestContext.Current.CancellationToken);

        Assert.Equal(0, CountContainers(job.JobId));
    }

    [Fact]
    public async Task Reports_a_container_that_cannot_start()
    {
        Assert.SkipUnless(DockerAvailable(), "Docker daemon not available");
        using var backend = new DockerJobBackend(new DockerBackendOptions { User = "no-such-user" });
        var job = NewJob("echo never");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => backend.RunAsync(job, new RecordingOutput(), TestContext.Current.CancellationToken));

        Assert.Contains("no-such-user", error.Message);
        Assert.Equal(0, CountContainers(job.JobId));
    }

    static JobSpec NewJob(string command) => new($"it-{Guid.NewGuid():N}", "integration", command);

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
        readonly MemoryStream bytes = new();
        readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Text => Encoding.UTF8.GetString(bytes.ToArray());
        public DateTimeOffset? StartedAt { get; private set; }
        public Task WhenStarted => started.Task;

        public void Started(DateTimeOffset startedAt)
        {
            StartedAt = startedAt;
            started.TrySetResult();
        }

        public void WaitingForNode() => throw new InvalidOperationException("Docker has no nodes.");
        public void Placed() => throw new InvalidOperationException("Docker has no nodes.");
        public void Write(ReadOnlySpan<byte> data) => bytes.Write(data);
    }
}
