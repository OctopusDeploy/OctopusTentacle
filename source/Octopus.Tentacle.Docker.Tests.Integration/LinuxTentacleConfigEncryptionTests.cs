using FluentAssertions;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
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

        RegisteredMachine registeredMachine;
        await using (var tentacle = await new TentacleContainerBuilder(server)
                         .WithVolume(config, "/etc/octopus")
                         .WithVolume(data, "/var/lib/octopus/tentacle")
                         .Build())
        {
            registeredMachine = tentacle.RegisteredMachine;
        }

        var inspectEncryption = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody("""
                            echo "key permissions $(stat -c %a /var/lib/octopus/tentacle/Tentacle/keys/config-encryption.key)"
                            echo "encrypted certificates $(grep -cF 'key="Tentacle.Certificate">AESGCM-V1$' /etc/octopus/tentacle.config)"
                            """)
            .Build();

        await using var recreatedTentacle = await new TentacleContainerBuilder(server)
            .WithVolume(config, "/etc/octopus")
            .WithVolume(data, "/var/lib/octopus/tentacle")
            .WithStartingAlreadyConfiguredTentacle(registeredMachine)
            .Build();
        var (result, output) = await recreatedTentacle.ExecuteScript(inspectEncryption);

        result.ExitCode.Should().Be(0, output);
        output.Should().Contain("key permissions 600");
        output.Should().Contain("encrypted certificates 1");
    }

    [Test]
    public async Task TheContainerRefusesToStartWhenOnlyTheConfigurationIsPersisted()
    {
        await using var config = await DockerVolume.Create(CancellationToken.None);
        await using var server = await new FakeOctopusServerBuilder().Build();

        await using var tentacle = await new TentacleContainerBuilder(server)
            .WithVolume(config, "/etc/octopus")
            .WithTentacleThatRefusesToStart()
            .Build();

        (await tentacle.Docker.GetLogs()).Should().Contain("/var/lib/octopus/tentacle is not");
    }
}
