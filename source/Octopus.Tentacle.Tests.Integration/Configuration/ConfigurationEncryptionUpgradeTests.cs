#if NET
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using CliWrap;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Integration.Configuration
{
    [LinuxTest]
    [UnsupportedOSPlatform("windows")]
    public class ConfigurationEncryptionUpgradeTests : IntegrationTest
    {
        static readonly string EncryptedWithItsOwnKey = LinuxMachineKeyEncryptor.Specifier + "$";

        [Test]
        public async Task AVersion14ConfigurationEncryptedWithTheMachineIdIsUpgradedToItsOwnKey()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var thumbprintBeforeUpgrade = await instance.ShowThumbprint();
            instance.CertificateAsStoredInTheConfigurationFile().Should().NotStartWith(EncryptedWithItsOwnKey, "version 14 encrypts with a key derived from the machine id");

            // Act
            instance.UpgradeToLatestTentacle();
            await instance.ExecuteTentacleCommand(CommandResultValidation.ZeroExitCode, "upgrade-configuration-encryption", "--instance", instance.Name);

            // Assert
            instance.CertificateAsStoredInTheConfigurationFile().Should().StartWith(EncryptedWithItsOwnKey);
            File.Exists(Path.Combine(instance.DataDirectory, instance.Name, "keys", ConfigurationEncryptionKey.KeyFileName)).Should().BeTrue();
            (await instance.ShowThumbprint()).Should().Be(thumbprintBeforeUpgrade, "the upgraded certificate must still decrypt to the same certificate");
        }

        [Test]
        public async Task WhenItsOwnKeyCannotBeCreatedThenTheUpgradeFailsAndAVersion14ConfigurationStaysEncryptedWithTheMachineId()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedByVersion14 = instance.CertificateAsStoredInTheConfigurationFile();
            await File.WriteAllTextAsync(instance.DataDirectory, "a file where the data directory should be, so the key directory cannot be created");

            // Act
            instance.UpgradeToLatestTentacle();
            var upgrade = await instance.ExecuteTentacleCommand(CommandResultValidation.None, "upgrade-configuration-encryption", "--instance", instance.Name);

            // Assert
            var output = upgrade.StandardOutput + upgrade.StandardError;
            upgrade.ExitCode.Should().NotBe(0, output);
            output.Should().Contain(instance.DataDirectory, "the failure should be about creating the key, not something else");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedByVersion14);
        }
    }
}
#endif
