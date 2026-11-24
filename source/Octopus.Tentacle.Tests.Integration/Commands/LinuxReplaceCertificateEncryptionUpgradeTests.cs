#if NET
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using CliWrap;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Integration.Commands
{
    [LinuxTest]
    [UnsupportedOSPlatform("windows")]
    public class LinuxReplaceCertificateEncryptionUpgradeTests : IntegrationTest
    {
        const string MachineName = "replace-certificate-test";

        [Test]
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndReplaceCertificateIsCalled_ThenTheNewCertificateIsEncryptedWithItsOwnKey()
        {
            // Arrange
            await using var octopus = await new FakeOctopusServerBuilder().Build();
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var server = $"--server=http://localhost:{octopus.Port}";
            await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "register-with", CancellationToken,
                server, "--apiKey=API-KEY", $"--space={FakeOctopusServerBuilder.SpaceName}", $"--environment={FakeOctopusServerBuilder.EnvironmentName}",
                "--role=test", $"--name={MachineName}", "--comms-style=TentaclePassive");

            // Act
            var upgradedInstance = instance.WithCurrentTentacleExe();
            await upgradedInstance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "replace-certificate", CancellationToken, server, "--apiKey=API-KEY");

            // Assert
            var newThumbprint = await upgradedInstance.ShowThumbprint(CancellationToken);
            newThumbprint.Should().NotBe(instance.Thumbprint);
            upgradedInstance.CertificateAsStoredInTheConfigurationFile().Should().StartWith(LinuxMachineKeyEncryptor.Specifier + "$");
            File.Exists(instance.ConfigurationEncryptionKeyFile).Should().BeTrue();
            octopus.LatestRegistration(FakeOctopusServerBuilder.SpaceName, MachineName)!.Thumbprint.Should().Be(newThumbprint);
        }
    }
}
#endif
