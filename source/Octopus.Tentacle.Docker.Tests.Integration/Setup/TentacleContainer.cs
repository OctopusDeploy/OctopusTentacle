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
        // Error is last because it is free text and may contain spaces.
        var state = await DockerCli.Run(new[] { "inspect", "--format", "{{.State.Status}}|{{.State.ExitCode}}|{{.State.OOMKilled}}|{{.State.Error}}", Id }, cancellationToken);
        var parts = state.Split('|', 4);
        if (parts[0] == "running")
        {
            return null;
        }

        var description = $"status '{parts[0]}', exit code {parts.ElementAtOrDefault(1)}";
        if (parts.ElementAtOrDefault(2) == "true")
        {
            description += ", killed for running out of memory";
        }

        if (!string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(3)))
        {
            description += $", error '{parts[3]}'";
        }

        return description;
    }

    /// <summary>"running" or how the container stopped. Never throws: this is called while reporting other failures.</summary>
    public async Task<string> DescribeState()
    {
        try
        {
            return await GetExitDescription(CancellationToken.None) ?? "running";
        }
        catch (Exception e)
        {
            return $"unknown ({e.Message})";
        }
    }

    public Task<string> GetLogs() => DockerCli.Logs(Id);

    /// <summary>Restarts the container as `docker restart` does: SIGTERM, then SIGKILL after the grace period.</summary>
    public Task Restart(TimeSpan gracePeriod, CancellationToken cancellationToken)
        => DockerCli.Run(new[] { "restart", "--time", ((int)gracePeriod.TotalSeconds).ToString(), Id }, cancellationToken);

    /// <summary>Stops the container as `docker stop` does: SIGTERM, then SIGKILL after the grace period.</summary>
    public Task Stop(TimeSpan gracePeriod, CancellationToken cancellationToken)
        => DockerCli.Run(new[] { "stop", "--time", ((int)gracePeriod.TotalSeconds).ToString(), Id }, cancellationToken);

    public async Task<int> GetExitCode(CancellationToken cancellationToken)
        => int.Parse(await DockerCli.Run(new[] { "inspect", "--format", "{{.State.ExitCode}}", Id }, cancellationToken));

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
