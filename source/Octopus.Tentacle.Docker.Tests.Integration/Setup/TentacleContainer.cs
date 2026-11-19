namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

public enum TentacleCommunicationStyle
{
    Listening,
    Polling
}

/// <summary>
/// A container running the Linux Tentacle image exactly as customers run it: the image's own entrypoint configures
/// the Tentacle, registers it with the (fake) server from environment variables, then starts the agent.
/// </summary>
public sealed class TentacleContainer : IAsyncDisposable
{
    /// <summary>Set to true to leave containers behind after the tests for debugging.</summary>
    public const string KeepContainersVariable = "TentacleDockerTests_KeepContainers";

    /// <summary>The port Tentacle listens on inside the container.</summary>
    public const int ListeningPort = 10933;

    /// <summary>How Tentacle containers address the machine running the tests.</summary>
    public const string DockerHost = "host.docker.internal";

    TentacleContainer(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }

    public static async Task<TentacleContainer> Start(
        string image,
        string name,
        TentacleCommunicationStyle communicationStyle,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "run",
            "--detach",
            "--name", name,
            "--hostname", name,
            "--label", DockerCli.ContainerLabel,
            // Docker Desktop provides host.docker.internal; this makes Linux engines provide it too.
            "--add-host", $"{DockerHost}:host-gateway"
        };

        if (communicationStyle == TentacleCommunicationStyle.Listening)
        {
            // Publish to a random loopback port: the tests connect to Tentacle from the host.
            arguments.AddRange(new[] { "--publish", $"127.0.0.1::{ListeningPort}" });
        }

        foreach (var (key, value) in environment)
        {
            arguments.AddRange(new[] { "--env", $"{key}={value}" });
        }

        arguments.Add(image);

        var id = await DockerCli.Run(arguments, cancellationToken);
        return new TentacleContainer(id, name);
    }

    /// <summary>The host port Docker published the container's listening port on.</summary>
    public async Task<int> GetPublishedListeningPort(CancellationToken cancellationToken)
    {
        // e.g. "127.0.0.1:49153", possibly followed by an IPv6 binding on another line.
        var output = await DockerCli.Run(new[] { "port", Id, $"{ListeningPort}/tcp" }, cancellationToken);
        var binding = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First();
        return int.Parse(binding[(binding.LastIndexOf(':') + 1)..]);
    }

    /// <summary>Null while the container is running, otherwise a description of how it stopped.</summary>
    public async Task<string?> GetExitDescription(CancellationToken cancellationToken)
    {
        var state = await DockerCli.Run(new[] { "inspect", "--format", "{{.State.Status}} {{.State.ExitCode}}", Id }, cancellationToken);
        var parts = state.Split(' ');
        return parts[0] == "running" ? null : $"status '{parts[0]}', exit code {parts.ElementAtOrDefault(1)}";
    }

    public Task<string> GetLogs() => DockerCli.Logs(Id);

    public async ValueTask DisposeAsync()
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable(KeepContainersVariable), out var keep) && keep)
        {
            TestContext.Progress.WriteLine($"Leaving container {Name} ({Id}) running because {KeepContainersVariable} is set");
            return;
        }

        try
        {
            await DockerCli.Run(new[] { "rm", "--force", "--volumes", Id }, CancellationToken.None);
        }
        catch (Exception e)
        {
            TestContext.Progress.WriteLine($"Could not remove container {Name} ({Id}): {e.Message}");
        }
    }
}
