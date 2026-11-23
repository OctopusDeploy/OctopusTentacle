using FluentAssertions;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// The image encrypts tentacle.config with a key generated in /var/lib/octopus/tentacle, so both must be persisted for
/// a re-created container to read its configuration.
/// </summary>
public class LinuxTentacleConfigEncryptionTests
{
    [Test]
    public async Task ARecreatedContainerDecryptsTheConfigurationWithThePersistedKey()
    {
        await using var config = await DockerVolume.Create(CancellationToken.None);
        await using var data = await DockerVolume.Create(CancellationToken.None);
        await using var server = await new FakeOctopusServerBuilder().Build();

        string? thumbprint;
        await using (var tentacle = await new TentacleContainerBuilder(server)
                         .WithVolume(config, "/etc/octopus")
                         .WithVolume(data, "/var/lib/octopus/tentacle")
                         .Build())
        {
            thumbprint = tentacle.RegisteredMachine.Thumbprint;
        }

        (await RunInImage("stat -c %a /var/lib/octopus/tentacle/Tentacle/keys/config-encryption.key", config, data)).Should().Be("600");
        (await RunInImage("grep -cF 'key=\"Tentacle.Certificate\">AESGCM-V1$' /etc/octopus/tentacle.config", config, data)).Should().Be("1");
        (await RunInImage("tentacle show-thumbprint --instance Tentacle", config, data)).Should().Be(thumbprint);
    }

    [Test]
    public async Task TheContainerRefusesToStartWhenOnlyTheConfigurationIsPersisted()
    {
        await using var config = await DockerVolume.Create(CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        var (exitCode, output) = await DockerCli.RunUnchecked(new[]
        {
            "run", "--rm", "--label", DockerCli.ContainerLabel,
            "--volume", config.MountAt("/etc/octopus"),
            "--env", "ACCEPT_EULA=Y",
            TentacleImage.Resolve()
        }, cancellationTokenSource.Token);

        exitCode.Should().NotBe(0);
        output.Should().Contain("/var/lib/octopus/tentacle is not");
    }

    static async Task<string> RunInImage(string command, DockerVolume config, DockerVolume data)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var (exitCode, output) = await DockerCli.RunUnchecked(new[]
        {
            "run", "--rm", "--label", DockerCli.ContainerLabel,
            "--volume", config.MountAt("/etc/octopus"),
            "--volume", data.MountAt("/var/lib/octopus/tentacle"),
            "--entrypoint", "bash",
            TentacleImage.Resolve(), "-c", command
        }, cancellationTokenSource.Token);

        TestContext.Out.WriteLine($"$ {command} (exit code {exitCode})\n{output}");
        exitCode.Should().Be(0, output);
        return output.Trim();
    }
}
