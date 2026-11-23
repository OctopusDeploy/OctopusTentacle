namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>A named volume, removed when disposed.</summary>
public sealed class DockerVolume : IAsyncDisposable
{
    DockerVolume(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static async Task<DockerVolume> Create(CancellationToken cancellationToken)
    {
        var name = $"tentacle-docker-test-{Guid.NewGuid():N}";
        await DockerCli.Run(new[] { "volume", "create", "--label", DockerCli.ContainerLabel, name }, cancellationToken);
        return new DockerVolume(name);
    }

    public string MountAt(string containerPath) => $"{Name}:{containerPath}";

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DockerCli.Run(new[] { "volume", "rm", "--force", Name }, CancellationToken.None);
        }
        catch (Exception e)
        {
            TestContext.Progress.WriteLine($"Could not remove volume {Name}: {e.Message}");
        }
    }
}
