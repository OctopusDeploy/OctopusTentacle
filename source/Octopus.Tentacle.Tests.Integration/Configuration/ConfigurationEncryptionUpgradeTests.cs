#if NET
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using CliWrap;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
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
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndUpgradeConfigurationEncryptionIsCalled_ThenTheCertificateIsReEncrypted()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var thumbprintBeforeUpgrade = await instance.ShowThumbprint();
            instance.CertificateAsStoredInTheConfigurationFile().Should().NotStartWith(EncryptedWithItsOwnKey, "version 14 encrypts with a key derived from the machine id");

            // Act
            var upgradedInstance = instance.WithCurrentTentacleExe();
            await upgradedInstance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "upgrade-configuration-encryption");

            // Assert
            instance.CertificateAsStoredInTheConfigurationFile().Should().StartWith(EncryptedWithItsOwnKey);
            File.Exists(instance.ConfigurationEncryptionKeyFile).Should().BeTrue();
            (await upgradedInstance.ShowThumbprint()).Should().Be(thumbprintBeforeUpgrade, "the upgraded certificate must still decrypt to the same certificate");
        }

        [Test]
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndTheNewKeyCannotBeCreated_ThenUpgradeConfigurationEncryptionFailsAndTheCertificateIsUnchanged()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedByVersion14 = instance.CertificateAsStoredInTheConfigurationFile();
            await File.WriteAllTextAsync(instance.DataDirectory, "a file where the data directory should be, so the key directory cannot be created");

            // Act
            var upgradedInstance = instance.WithCurrentTentacleExe();
            var upgrade = await upgradedInstance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "upgrade-configuration-encryption");

            // Assert
            var output = upgrade.StandardOutput + upgrade.StandardError;
            upgrade.ExitCode.Should().NotBe(0, output);
            output.Should().Contain(instance.DataDirectory, "the failure should be about creating the key, not something else");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedByVersion14);
        }

        [Test]
        public async Task WhenTentacleInstanceIsUnnamed_AndUpgradeConfigurationEncryptionIsCalled_ThenItFailsAndSaysToCreateANamedInstance()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .UnnamedInstanceAddressedOnlyByConfig()
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedByVersion14 = instance.CertificateAsStoredInTheConfigurationFile();

            // Act
            var upgradedInstance = instance.WithCurrentTentacleExe();
            var upgrade = await upgradedInstance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "upgrade-configuration-encryption");

            // Assert
            var output = upgrade.StandardOutput + upgrade.StandardError;
            upgrade.ExitCode.Should().NotBe(0, output);
            output.Should().Contain("create-instance --instance", "the user should be told how to give the configuration a name");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedByVersion14);
        }

        [Test]
        public async Task WhenTentacleKeyFileIsDeleted_AndANewCertificateIsCreated_ThenItFailsRatherThanFallBackToEtcMachineId()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedWithTheKey = instance.CertificateAsStoredInTheConfigurationFile();
            certificateEncryptedWithTheKey.Should().StartWith(EncryptedWithItsOwnKey);
            File.Delete(instance.ConfigurationEncryptionKeyFile);

            // Act
            var newCertificate = await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "new-certificate");

            // Assert
            var output = newCertificate.StandardOutput + newCertificate.StandardError;
            newCertificate.ExitCode.Should().NotBe(0, output);
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedWithTheKey);
        }

        [Test]
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndTheCurrentTentacleStarts_ThenTheCertificateIsReEncrypted()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);

            // Act
            await using var runningTentacle = await new ListeningRunningTentacleBuilder(TestCertificates.ServerPublicThumbprint, instance.WithCurrentTentacleExe())
                .Build(Logger, CancellationToken);

            // Assert
            instance.CertificateAsStoredInTheConfigurationFile().Should().StartWith(EncryptedWithItsOwnKey);
            (await instance.WithCurrentTentacleExe().ShowThumbprint()).Should().Be(instance.Thumbprint, "the upgraded certificate must still decrypt to the same certificate");
        }

        [Test]
        public async Task WhenTentacleKeyFileIsDeleted_AndTheCurrentTentacleStarts_ThenTheCertificateIsLeftUntouched()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedWithTheKey = instance.CertificateAsStoredInTheConfigurationFile();
            File.Delete(instance.ConfigurationEncryptionKeyFile);

            // Act
            Func<Task> start = async () =>
            {
                await using var runningTentacle = await new ListeningRunningTentacleBuilder(TestCertificates.ServerPublicThumbprint, instance)
                    .Build(Logger, CancellationToken);
            };

            // Assert
            await start.Should().ThrowAsync<Exception>("the certificate cannot be decrypted without its key");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedWithTheKey);
        }
    }
}
#endif
