using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Docker;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class DockerJobBackendTests
{
    static readonly JobSpec Job = new("job-1", "run-1", "echo hi");

    static DockerJobBackend CreateBackend(FakeDocker docker) => new(new DockerBackendOptions(), TimeProvider.System, docker);

    [Fact]
    public async Task Refuses_sandboxed_jobs()
    {
        var docker = new FakeDocker();
        using var backend = CreateBackend(docker);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            backend.RunAsync(Job with { Isolation = JobIsolation.Sandboxed }, new RecordingOutput(docker.Log), TestContext.Current.CancellationToken));

        Assert.Empty(docker.Requests);
    }

    [Fact]
    public async Task Creates_a_locked_down_container()
    {
        var docker = new FakeDocker();
        using var backend = CreateBackend(docker);

        await backend.RunAsync(Job, new RecordingOutput(docker.Log), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(docker.CreateBody!);
        var root = body.RootElement;
        var host = root.GetProperty("HostConfig");
        Assert.Equal("none", host.GetProperty("NetworkMode").GetString());
        Assert.True(host.GetProperty("ReadonlyRootfs").GetBoolean());
        Assert.Equal(["ALL"], host.GetProperty("CapDrop").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["no-new-privileges"], host.GetProperty("SecurityOpt").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(33554432, host.GetProperty("Memory").GetInt64());
        Assert.Equal(250000000, host.GetProperty("NanoCpus").GetInt64());
        Assert.Equal("65534:65534", root.GetProperty("User").GetString());
        Assert.Equal("busybox:1.37", root.GetProperty("Image").GetString());
        Assert.Equal(["sh", "-c", "echo hi"], root.GetProperty("Cmd").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("job-1", root.GetProperty("Labels").GetProperty("octopus.job-id").GetString());
        Assert.Equal("run-1", root.GetProperty("Labels").GetProperty("octopus.run-id").GetString());
    }

    [Fact]
    public async Task Attaches_before_starting()
    {
        var docker = new FakeDocker();
        using var backend = CreateBackend(docker);

        await backend.RunAsync(Job, new RecordingOutput(docker.Log), TestContext.Current.CancellationToken);

        Assert.Equal(["inspect", "create", "attach", "start", "wait", "delete"], docker.Steps);
    }

    [Fact]
    public async Task Demultiplexes_stdout_and_stderr_in_order()
    {
        // Delivered a byte at a time, so every header and payload is split across reads.
        var docker = new FakeDocker { AttachStream = [.. Frame(1, "out1"), .. Frame(2, "err1"), .. Frame(1, "out2")], TrickleAttach = true };
        using var backend = CreateBackend(docker);
        var output = new RecordingOutput(docker.Log);

        await backend.RunAsync(Job, output, TestContext.Current.CancellationToken);

        Assert.Equal("out1err1out2", output.Text);
    }

    [Fact]
    public async Task Pulls_a_missing_image_before_reporting_started()
    {
        var docker = new FakeDocker { ImageMissing = true };
        using var backend = CreateBackend(docker);

        await backend.RunAsync(Job, new RecordingOutput(docker.Log), TestContext.Current.CancellationToken);

        var pull = docker.Log.IndexOf("pull");
        var start = docker.Log.IndexOf("start");
        var started = docker.Log.IndexOf("Started");
        Assert.True(pull >= 0 && pull < start, string.Join(",", docker.Log));
        Assert.True(start < started, string.Join(",", docker.Log));
        Assert.Contains("images/create?fromImage=busybox%3A1.37", docker.Requests.Single(r => r.StartsWith("POST images/create")));
    }

    [Fact]
    public async Task Removes_the_container_when_cancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var docker = new FakeDocker { BlockWait = true, OnWait = cts.Cancel };
        using var backend = CreateBackend(docker);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => backend.RunAsync(Job, new RecordingOutput(docker.Log), cts.Token));

        Assert.Equal(["wait", "stop", "delete"], docker.Steps.TakeLast(3));
        Assert.Contains("POST containers/c1/stop?t=5", docker.Requests);
        Assert.Contains("DELETE containers/c1?force=1", docker.Requests);
    }

    [Fact]
    public async Task Removes_the_container_when_cancelled_during_create()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var docker = new FakeDocker { OnCreate = cts.Cancel };
        using var backend = CreateBackend(docker);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => backend.RunAsync(Job, new RecordingOutput(docker.Log), cts.Token));

        Assert.Equal(["create", "stop", "delete"], docker.Steps.TakeLast(3));
        Assert.Contains("DELETE containers/c1?force=1", docker.Requests);
        Assert.DoesNotContain("Started", docker.Log);
    }

    [Fact]
    public async Task Removes_the_container_when_the_job_fails()
    {
        var docker = new FakeDocker { ExitCode = 2 };
        using var backend = CreateBackend(docker);

        var exitCode = await backend.RunAsync(Job, new RecordingOutput(docker.Log), TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Contains("DELETE containers/c1?force=1", docker.Requests);
    }

    [Fact]
    public async Task Keeps_the_exit_code_when_removing_the_container_fails()
    {
        var docker = new FakeDocker { FailDelete = true };
        using var backend = CreateBackend(docker);

        var exitCode = await backend.RunAsync(Job, new RecordingOutput(docker.Log), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("DELETE containers/c1?force=1", docker.Requests);
    }

    [Fact]
    public async Task Keeps_the_cancellation_when_removing_the_container_fails()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var docker = new FakeDocker { BlockWait = true, OnWait = cts.Cancel, FailDelete = true };
        using var backend = CreateBackend(docker);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => backend.RunAsync(Job, new RecordingOutput(docker.Log), cts.Token));

        Assert.Contains("DELETE containers/c1?force=1", docker.Requests);
    }

    [Fact]
    public async Task Names_the_socket_when_the_daemon_is_unreachable()
    {
        using var backend = new DockerJobBackend(new DockerBackendOptions { Endpoint = "unix:///nonexistent.sock" });
        var stopwatch = Stopwatch.StartNew();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.RunAsync(Job, new RecordingOutput([]), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Contains("/nonexistent.sock", error.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    static byte[] Frame(byte stream, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var frame = new byte[8 + payload.Length];
        frame[0] = stream;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(frame, 8);
        return frame;
    }

    /// <summary>A fake Docker Engine API that records each call.</summary>
    sealed class FakeDocker : HttpMessageHandler
    {
        public List<string> Log { get; } = [];
        public List<string> Steps { get; } = [];
        public List<string> Requests { get; } = [];
        public string? CreateBody { get; private set; }
        public bool ImageMissing { get; init; }
        public byte[] AttachStream { get; init; } = [];
        public bool TrickleAttach { get; init; }
        public int ExitCode { get; init; }
        public bool BlockWait { get; init; }
        public Action? OnWait { get; init; }
        public Action? OnCreate { get; init; }
        public bool FailDelete { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Assert.StartsWith("/v1.44/", uri.AbsolutePath);
            var path = uri.AbsolutePath["/v1.44/".Length..];
            lock (Requests)
            {
                Requests.Add($"{request.Method} {path}{uri.Query}");
            }

            var step = (request.Method.Method, path) switch
            {
                ("GET", "images/busybox:1.37/json") => "inspect",
                ("POST", "images/create") => "pull",
                ("POST", "containers/create") => "create",
                ("POST", "containers/c1/attach") => "attach",
                ("POST", "containers/c1/start") => "start",
                ("POST", "containers/c1/wait") => "wait",
                ("POST", "containers/c1/stop") => "stop",
                ("DELETE", "containers/c1") => "delete",
                _ => throw new InvalidOperationException($"Unexpected request {request.Method} {uri}"),
            };
            lock (Log)
            {
                Steps.Add(step);
                Log.Add(step);
            }

            switch (step)
            {
                case "inspect":
                    return ImageMissing ? Json(HttpStatusCode.NotFound, """{"message":"No such image: busybox:1.37"}""") : Json(HttpStatusCode.OK, "{}");
                case "pull":
                    return Json(HttpStatusCode.OK, "{\"status\":\"Pulling\"}\n{\"status\":\"Done\"}\n");
                case "create":
                    CreateBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                    OnCreate?.Invoke();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Json(HttpStatusCode.Created, """{"Id":"c1","Warnings":[]}""");
                case "attach":
                    Stream body = TrickleAttach ? new TrickleStream(AttachStream) : new MemoryStream(AttachStream);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
                case "start":
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                case "wait":
                    OnWait?.Invoke();
                    if (BlockWait)
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }

                    return Json(HttpStatusCode.OK, $$"""{"StatusCode":{{ExitCode}}}""");
                case "delete" when FailDelete:
                    return Json(HttpStatusCode.InternalServerError, """{"message":"removal of container c1 is already in progress"}""");
                default:
                    cancellationToken.ThrowIfCancellationRequested();
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    sealed class RecordingOutput(List<string> log) : IJobOutput
    {
        readonly MemoryStream bytes = new();

        public string Text => Encoding.UTF8.GetString(bytes.ToArray());

        public void Started(DateTimeOffset startedAt)
        {
            lock (log)
            {
                log.Add("Started");
            }
        }

        public void WaitingForNode() => throw new InvalidOperationException("Docker has no nodes.");
        public void Placed() => throw new InvalidOperationException("Docker has no nodes.");
        public void Write(ReadOnlySpan<byte> data) => bytes.Write(data);
    }

    /// <summary>Returns at most one byte per read.</summary>
    sealed class TrickleStream(byte[] data) : Stream
    {
        int position;

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (position >= data.Length || buffer.IsEmpty)
            {
                return 0;
            }

            buffer[0] = data[position++];
            return 1;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer.AsSpan(offset, count)));

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
