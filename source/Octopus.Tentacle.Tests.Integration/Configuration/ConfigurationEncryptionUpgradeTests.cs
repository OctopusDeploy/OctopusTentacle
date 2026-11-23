#if NET
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using CliWrap;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Tests.Integration.Common;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

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
            var thumbprintBeforeUpgrade = await instance.ShowThumbprint(CancellationToken);
            instance.CertificateAsStoredInTheConfigurationFile().Should().NotStartWith(EncryptedWithItsOwnKey, "version 14 encrypts with a key derived from the machine id");

            // Act
            var upgradedInstance = instance.WithCurrentTentacleExe();
            await upgradedInstance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "upgrade-configuration-encryption", CancellationToken);

            // Assert
            instance.CertificateAsStoredInTheConfigurationFile().Should().StartWith(EncryptedWithItsOwnKey);
            File.Exists(instance.ConfigurationEncryptionKeyFile).Should().BeTrue();
            (await upgradedInstance.ShowThumbprint(CancellationToken)).Should().Be(thumbprintBeforeUpgrade, "the upgraded certificate must still decrypt to the same certificate");
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
            var upgrade = await upgradedInstance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "upgrade-configuration-encryption", CancellationToken);

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
            var upgrade = await upgradedInstance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "upgrade-configuration-encryption", CancellationToken);

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
            var newCertificate = await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "new-certificate", CancellationToken);

            // Assert
            var output = newCertificate.StandardOutput + newCertificate.StandardError;
            newCertificate.ExitCode.Should().NotBe(0, output);
            output.Should().Contain("configuration encryption key, which is not available", "the failure should be about the missing key, not something else");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedWithTheKey);
        }

        [Test]
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndTheCurrentTentacleStarts_ThenTheCertificateIsReEncrypted()
        {
            // Arrange
            await using var runningTentacle = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .RunAsListeningTentacle(TestCertificates.ServerPublicThumbprint)
                .Build(Logger, CancellationToken);
            runningTentacle.Instance.CertificateAsStoredInTheConfigurationFile().Should().NotStartWith(EncryptedWithItsOwnKey, "version 14 encrypts with a key derived from the machine id");

            // Act
            await runningTentacle.StopAndStartWithCurrentVersionOfTentacle(CancellationToken);

            // Assert
            runningTentacle.Instance.CertificateAsStoredInTheConfigurationFile().Should().StartWith(EncryptedWithItsOwnKey);
            (await runningTentacle.Instance.ShowThumbprint(CancellationToken)).Should().Be(runningTentacle.Thumbprint, "the upgraded certificate must still decrypt to the same certificate");
        }

        [Test]
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndTheNewKeyCannotBeCreated_ThenTheCurrentTentacleStillStartsAndTheCertificateIsUnchanged()
        {
            // Arrange
            await using var runningTentacle = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .RunAsListeningTentacle(TestCertificates.ServerPublicThumbprint)
                .Build(Logger, CancellationToken);
            var certificateEncryptedByVersion14 = runningTentacle.Instance.CertificateAsStoredInTheConfigurationFile();
            await File.WriteAllTextAsync(runningTentacle.Instance.DataDirectory, "a file where the data directory should be, so the key directory cannot be created");

            // Act
            await runningTentacle.StopAndStartWithCurrentVersionOfTentacle(CancellationToken);

            // Assert
            await using var serverHalibut = TestServerHalibut.BuildServerHalibutRuntime();
            var tentacleClient = TestServerHalibut.BuildTentacleClient(runningTentacle.ServiceUri, runningTentacle.Thumbprint, serverHalibut, _ => { });
            var (scriptResult, _) = await tentacleClient.ExecuteScript(new TestExecuteShellScriptCommandBuilder().SetScriptBody(b => b.Print("Hello")).Build(), CancellationToken);
            scriptResult.State.Should().Be(ProcessState.Complete);
            scriptResult.ExitCode.Should().Be(0);

            runningTentacle.Instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedByVersion14);
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
            var agent = await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "agent", CancellationToken);

            // Assert
            var output = agent.StandardOutput + agent.StandardError;
            agent.ExitCode.Should().NotBe(0, output);
            output.Should().Contain(instance.ConfigurationEncryptionKeyFile, "the certificate cannot be decrypted without its key");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedWithTheKey);
        }
    }
}
#endif
