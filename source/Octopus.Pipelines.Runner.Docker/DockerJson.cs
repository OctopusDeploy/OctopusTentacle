using System.Text.Json.Serialization;

namespace Octopus.Pipelines.Runner.Docker;

// The subset of Engine API v1.44 bodies the backend uses. Property names match the API's.

sealed class ContainerCreateRequest
{
    public required string Image { get; init; }
    public required string[] Cmd { get; init; }
    public required string User { get; init; }
    public required Dictionary<string, string> Labels { get; init; }
    public bool AttachStdin { get; init; }
    public bool AttachStdout { get; init; } = true;
    public bool AttachStderr { get; init; } = true;
    public bool Tty { get; init; }
    public bool OpenStdin { get; init; }
    public required HostConfig HostConfig { get; init; }
}

sealed class HostConfig
{
    public required string NetworkMode { get; init; }
    public long Memory { get; init; }
    public long NanoCpus { get; init; }
    public bool ReadonlyRootfs { get; init; }
    public required string[] CapDrop { get; init; }
    public required string[] SecurityOpt { get; init; }
}

sealed class ContainerCreateResponse
{
    public string Id { get; init; } = "";
}

sealed class ContainerWaitResponse
{
    public long StatusCode { get; init; }
    public ContainerWaitError? Error { get; init; }
}

sealed class ContainerWaitError
{
    public string? Message { get; init; }
}

/// <summary>The daemon's error body, and also one line of an image pull's progress stream.</summary>
sealed class DockerMessage
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ContainerCreateRequest))]
[JsonSerializable(typeof(ContainerCreateResponse))]
[JsonSerializable(typeof(ContainerWaitResponse))]
[JsonSerializable(typeof(DockerMessage))]
partial class DockerJson : JsonSerializerContext;
