namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>A container started with `docker run`, removed when disposed.</summary>
public sealed class DockerContainer : IAsyncDisposable
{
    /// <summary>Set to true to leave containers behind after the tests for debugging.</summary>
    public const string KeepContainersVariable = "TentacleDockerTests_KeepContainers";

    DockerContainer(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>Starts a detached container from <paramref name="image"/> with the given `docker run` options.</summary>
    public static async Task<DockerContainer> Run(string image, string name, IEnumerable<string> runOptions, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "run",
            "--detach",
            "--name", name,
            "--label", DockerCli.ContainerLabel
        };
        arguments.AddRange(runOptions);
        arguments.Add(image);

        var id = await DockerCli.Run(arguments, cancellationToken);
        return new DockerContainer(id, name);
    }

    /// <summary>The loopback host port Docker published <paramref name="containerPort"/> on.</summary>
    public async Task<int> GetPublishedPort(int containerPort, CancellationToken cancellationToken)
    {
        // e.g. "127.0.0.1:49153", possibly followed by an IPv6 binding on another line.
        var output = await DockerCli.Run(new[] { "port", Id, $"{containerPort}/tcp" }, cancellationToken);
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
