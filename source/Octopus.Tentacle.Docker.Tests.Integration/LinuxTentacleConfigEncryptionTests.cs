using FluentAssertions;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// The image encrypts tentacle.config with a key generated in /var/lib/octopus/tentacle, so both must be persisted for
/// a re-created container to read its configuration.
/// </summary>
public class LinuxTentacleConfigEncryptionTests
{
    const string ConfigDirectory = "/etc/octopus";
    const string DataDirectory = "/var/lib/octopus/tentacle";
    const string KeyFile = DataDirectory + "/Tentacle/keys/config-encryption.key";

    static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(10);

    [Test]
    public async Task ARecreatedContainerDecryptsTheConfigurationWithThePersistedKey()
    {
        await using var config = await DockerVolume.Create(CancellationToken.None);
        await using var data = await DockerVolume.Create(CancellationToken.None);
        await using var server = await new FakeOctopusServerBuilder().Build();

        string? thumbprint;
        await using (var tentacle = await new TentacleContainerBuilder(server)
                         .WithVolume(config, ConfigDirectory)
                         .WithVolume(data, DataDirectory)
                         .Build())
        {
            thumbprint = tentacle.RegisteredMachine.Thumbprint;
        }

        var image = TentacleImage.Resolve();
        (await RunInImage(image, $"stat -c %a {KeyFile}", config, data)).Should().Be("600");
        (await RunInImage(image, $"grep -cF 'key=\"Tentacle.Certificate\">AESGCM-V1$' {ConfigDirectory}/tentacle.config", config, data)).Should().Be("1");
        (await RunInImage(image, "tentacle show-thumbprint --instance Tentacle", config, data)).Should().Be(thumbprint);
    }

    [Test]
    public async Task TheContainerRefusesToStartWhenOnlyTheConfigurationIsPersisted()
    {
        await using var config = await DockerVolume.Create(CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);

        var (exitCode, output) = await DockerCli.RunUnchecked(new[]
        {
            "run", "--rm", "--label", DockerCli.ContainerLabel,
            "--volume", config.MountAt(ConfigDirectory),
            "--env", "ACCEPT_EULA=Y",
            TentacleImage.Resolve()
        }, cancellationTokenSource.Token);

        exitCode.Should().NotBe(0);
        output.Should().Contain($"{DataDirectory} is not");
    }

    static async Task<string> RunInImage(string image, string command, DockerVolume config, DockerVolume data)
    {
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);
        var arguments = new List<string>
        {
            "run", "--rm", "--label", DockerCli.ContainerLabel,
            "--volume", config.MountAt(ConfigDirectory),
            "--volume", data.MountAt(DataDirectory),
            "--entrypoint", "bash"
        };
        arguments.AddRange(new[] { image, "-c", command });

        var (exitCode, output) = await DockerCli.RunUnchecked(arguments, cancellationTokenSource.Token);
        TestContext.Out.WriteLine($"$ {command} (exit code {exitCode})\n{output}");
        exitCode.Should().Be(0, output);

        return output.Trim();
    }
}
