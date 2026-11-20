using System.IO.Pipes;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>The few Docker Engine API v1.44 calls the backend makes, over the daemon's local socket.</summary>
sealed class DockerEngineClient : IDisposable
{
    readonly HttpClient http;
    readonly string endpoint;

    public DockerEngineClient(string endpoint, HttpMessageHandler? handler = null)
    {
        this.endpoint = endpoint;
        http = handler is null
            ? new HttpClient(CreateSocketHandler(endpoint), disposeHandler: true)
            : new HttpClient(handler, disposeHandler: false);

        // The host part is ignored: every connection goes to the socket.
        http.BaseAddress = new Uri("http://docker/v1.44/");

        // wait and attach last as long as the job; callers cancel instead.
        http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<bool> ImageExistsAsync(string image, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"images/{image}/json", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public async Task PullImageAsync(string image, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"images/create?fromImage={Uri.EscapeDataString(image)}", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        // The pull runs while the daemon streams JSON progress lines; a failure arrives as an "error" line with a 200.
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken), Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var progress = JsonSerializer.Deserialize(line, DockerJson.Default.DockerMessage);
            if (progress?.Error is { } error)
            {
                throw new InvalidOperationException($"Couldn't pull {image}: {error}");
            }
        }
    }

    public async Task<string> CreateContainerAsync(ContainerCreateRequest request, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(request, DockerJson.Default.ContainerCreateRequest);
        using var response = await SendAsync(HttpMethod.Post, "containers/create", content, cancellationToken);
        var created = await ReadAsync(response, DockerJson.Default.ContainerCreateResponse, cancellationToken);
        return string.IsNullOrEmpty(created.Id) ? throw new InvalidOperationException("Docker created a container without an ID.") : created.Id;
    }

    /// <summary>
    /// Attaches to the container's stdout and stderr. The daemon answers with headers straight away and then streams
    /// frames until the container exits, so attaching before <see cref="StartAsync"/> loses no output.
    /// </summary>
    public async Task<HttpResponseMessage> AttachAsync(string id, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Post, $"containers/{id}/attach?stream=1&stdout=1&stderr=1", null, cancellationToken);
        try
        {
            await EnsureSuccessAsync(response, cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task StartAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"containers/{id}/start", null, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotModified)
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    /// <summary>Waits for the container to exit and returns its exit code.</summary>
    public async Task<int> WaitAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"containers/{id}/wait", null, cancellationToken);
        var result = await ReadAsync(response, DockerJson.Default.ContainerWaitResponse, cancellationToken);
        if (result.Error?.Message is { Length: > 0 } message)
        {
            throw new InvalidOperationException($"Docker couldn't wait for container {id}: {message}");
        }

        return (int)result.StatusCode;
    }

    public async Task StopAsync(string id, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"containers/{id}/stop?t={timeoutSeconds}", null, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.NotModified or HttpStatusCode.NotFound))
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"containers/{id}?force=1", null, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    public void Dispose() => http.Dispose();

    async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException e) when (e.HttpRequestError == HttpRequestError.ConnectionError)
        {
            throw new InvalidOperationException($"Can't reach the Docker daemon at {endpoint}: {e.InnerException?.Message ?? e.Message}", e);
        }
    }

    static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken)
            ?? throw new InvalidOperationException($"Docker returned an empty body for {response.RequestMessage?.RequestUri}.");
    }

    static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? message = null;
        try
        {
            message = JsonSerializer.Deserialize(body, DockerJson.Default.DockerMessage)?.Message;
        }
        catch (JsonException)
        {
        }

        var request = response.RequestMessage;
        throw new InvalidOperationException(
            $"Docker {request?.Method} {request?.RequestUri?.PathAndQuery} failed with {(int)response.StatusCode}: {message ?? body}");
    }

    static SocketsHttpHandler CreateSocketHandler(string endpoint)
    {
        Func<CancellationToken, ValueTask<Stream>> connect;
        if (endpoint.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            var path = endpoint["unix://".Length..];
            connect = async cancellationToken =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            };
        }
        else if (endpoint.StartsWith("npipe://", StringComparison.OrdinalIgnoreCase))
        {
            // npipe://./pipe/docker_engine → server ".", pipe "docker_engine".
            var parts = endpoint["npipe://".Length..].Split('/', 3);
            if (parts is not [var server, "pipe", var name])
            {
                throw new ArgumentException($"Unrecognised Docker endpoint {endpoint}; expected npipe://<server>/pipe/<name>.", nameof(endpoint));
            }

            connect = async cancellationToken =>
            {
                var pipe = new NamedPipeClientStream(server, name, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(cancellationToken);
                    return pipe;
                }
                catch
                {
                    await pipe.DisposeAsync();
                    throw;
                }
            };
        }
        else
        {
            throw new ArgumentException($"Unrecognised Docker endpoint {endpoint}; expected unix:// or npipe://.", nameof(endpoint));
        }

        return new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = (_, cancellationToken) => connect(cancellationToken),
        };
    }
}
