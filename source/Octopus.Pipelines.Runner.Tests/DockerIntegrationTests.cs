using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Docker;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>Runs real containers on the local Docker daemon; skipped where there isn't one.</summary>
public sealed class DockerIntegrationTests
{
    const string SocketPath = "/var/run/docker.sock";

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
        Assert.Equal(1, await CountContainersAsync(job.JobId));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(0, await CountContainersAsync(job.JobId));
    }

    static JobSpec NewJob(string command) => new($"it-{Guid.NewGuid():N}", "integration", command);

    static bool DockerAvailable()
    {
        if (!File.Exists(SocketPath))
        {
            return false;
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(SocketPath));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    static async Task<int> CountContainersAsync(string jobId)
    {
        using var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
        using var http = new HttpClient(handler);
        var filters = Uri.EscapeDataString($$"""{"label":["octopus.job-id={{jobId}}"]}""");
        var json = await http.GetStringAsync($"http://docker/v1.44/containers/json?all=1&filters={filters}", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetArrayLength();
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
