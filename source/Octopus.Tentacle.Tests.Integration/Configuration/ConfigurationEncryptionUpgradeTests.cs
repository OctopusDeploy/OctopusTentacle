#if NET
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util;

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
            var thumbprintBeforeUpgrade = await Thumbprint(instance, instance.TentacleExe);
            EncryptedCertificateIn(instance).Should().NotStartWith(EncryptedWithItsOwnKey, "version 14 encrypts with a key derived from the machine id");

            // Act
            await instance.RunWith(TentacleExeFinder.FindTentacleExe(), "upgrade-configuration-encryption", "--instance", instance.Name);

            // Assert
            EncryptedCertificateIn(instance).Should().StartWith(EncryptedWithItsOwnKey);
            File.Exists(Path.Combine(instance.DataDirectory, instance.Name, "keys", ConfigurationEncryptionKey.KeyFileName)).Should().BeTrue();
            (await Thumbprint(instance, TentacleExeFinder.FindTentacleExe())).Should().Be(thumbprintBeforeUpgrade, "the upgraded certificate must still decrypt to the same certificate");
        }

        [Test]
        public async Task WhenItsOwnKeyCannotBeCreatedThenTheUpgradeFailsAndAVersion14ConfigurationStaysEncryptedWithTheMachineId()
        {
            // Arrange
            using var instance = await new TentacleInstanceBuilder()
                .WithTentacleVersion(TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption)
                .WithNewCertificate()
                .Build(Logger, CancellationToken);
            var certificateEncryptedByVersion14 = EncryptedCertificateIn(instance);
            await File.WriteAllTextAsync(instance.DataDirectory, "a file where the data directory should be, so the key directory cannot be created");

            // Act
            var upgrade = await instance.TryRunWith(TentacleExeFinder.FindTentacleExe(), "upgrade-configuration-encryption", "--instance", instance.Name);

            // Assert
            var output = upgrade.StandardOutput + upgrade.StandardError;
            upgrade.ExitCode.Should().NotBe(0, output);
            output.Should().Contain(instance.DataDirectory, "the failure should be about creating the key, not something else");
            EncryptedCertificateIn(instance).Should().Be(certificateEncryptedByVersion14);
        }

        static async Task<string> Thumbprint(TentacleInstance instance, string tentacleExe)
            => (await instance.RunWith(tentacleExe, "show-thumbprint", "--instance", instance.Name)).StandardOutput.Trim();

        static string EncryptedCertificateIn(TentacleInstance instance)
            => XDocument.Load(instance.ConfigurationFile).Root!.Elements("set").Single(e => (string?)e.Attribute("key") == "Tentacle.Certificate").Value;
    }
}
#endif
