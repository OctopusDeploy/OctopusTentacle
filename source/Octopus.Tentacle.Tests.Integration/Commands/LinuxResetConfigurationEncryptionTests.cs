#if NET
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;
using CliWrap;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Integration.Commands
{
    [LinuxTest]
    [UnsupportedOSPlatform("windows")]
    public class LinuxResetConfigurationEncryptionTests : IntegrationTest
    {
        [Test]
        public async Task WhenTheKeyIsLost_AndResetConfigurationEncryptionIsCalled_ThenProtectedSettingsAreRemovedAndANewKeyIsCreated()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "polling-proxy", CancellationToken,
                "--proxyEnable=true", "--proxyHost=proxy.example", "--proxyPort=8080", "--proxyUsername=user", "--proxyPassword=secret");
            File.Delete(instance.ConfigurationEncryptionKeyFile);

            // Act
            var reset = await instance.ExecuteTentacleCommandOnTheInstance(CommandResultValidation.ZeroExitCode, "reset-configuration-encryption", CancellationToken);

            // Assert
            File.Exists(instance.ConfigurationEncryptionKeyFile).Should().BeTrue();
            var settings = XDocument.Load(instance.ConfigurationFile).Root!.Elements("set")
                .ToDictionary(e => (string)e.Attribute("key")!, e => e.Value);
            settings.Should().NotContainKey(TentacleConfiguration.CertificateSettingName);
            settings.Should().NotContainKey(PollingProxyConfiguration.ProxyPasswordSettingName);
            settings[TentacleConfiguration.CertificateThumbprintSettingName].Should().Be(instance.Thumbprint);
            reset.StandardOutput.Should().Contain(instance.Thumbprint);
        }

        [Test]
        public async Task WhenTentacleKeyIsEncryptedWithEtcMachineId_AndResetConfigurationEncryptionIsCalled_ThenItRefusesAndTheCertificateIsUnchanged()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedByVersion14 = instance.CertificateAsStoredInTheConfigurationFile();

            // Act
            var reset = await instance.WithCurrentTentacleExe().ExecuteTentacleCommandOnTheInstance(CommandResultValidation.None, "reset-configuration-encryption", CancellationToken);

            // Assert
            var output = reset.StandardOutput + reset.StandardError;
            reset.ExitCode.Should().NotBe(0, output);
            output.Should().Contain("upgrade-configuration-encryption");
            instance.CertificateAsStoredInTheConfigurationFile().Should().Be(certificateEncryptedByVersion14);
        }
    }
}
#endif
