#if NET
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;
using CliWrap;
using CliWrap.Buffered;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
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
            using var instance = new IsolatedTentacleInstance();
            var version14 = await GetVersion14Tentacle();
            await instance.CreateWithACertificate(version14);
            var thumbprintBeforeUpgrade = await instance.Thumbprint(version14);
            instance.EncryptedCertificate.Should().NotStartWith(EncryptedWithItsOwnKey, "version 14 encrypts with a key derived from the machine id");

            // Act
            await instance.UpgradeConfigurationEncryption(TentacleExeFinder.FindTentacleExe());

            // Assert
            instance.EncryptedCertificate.Should().StartWith(EncryptedWithItsOwnKey);
            File.Exists(instance.KeyFile).Should().BeTrue();
            (await instance.Thumbprint(TentacleExeFinder.FindTentacleExe())).Should().Be(thumbprintBeforeUpgrade, "the upgraded certificate must still decrypt to the same certificate");
        }

        [Test]
        public async Task WhenItsOwnKeyCannotBeCreatedThenTheUpgradeFailsAndAVersion14ConfigurationStaysEncryptedWithTheMachineId()
        {
            // Arrange
            using var instance = new IsolatedTentacleInstance();
            var version14 = await GetVersion14Tentacle();
            await instance.CreateWithACertificate(version14);
            var certificateEncryptedByVersion14 = instance.EncryptedCertificate;
            instance.PreventTheKeyDirectoryFromBeingCreated();

            // Act
            var upgrade = await instance.TryUpgradeConfigurationEncryption(TentacleExeFinder.FindTentacleExe());

            // Assert
            upgrade.ExitCode.Should().NotBe(0, upgrade.Output);
            upgrade.Output.Should().Contain(instance.DataDirectory, "the failure should be about creating the key, not something else");
            instance.EncryptedCertificate.Should().Be(certificateEncryptedByVersion14);
        }

        Task<string> GetVersion14Tentacle()
            => TentacleFetcher.GetTentacleVersion(Path.GetTempPath(), TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption, DefaultTentacleRuntime.Value, Logger, CancellationToken);

        // A Tentacle instance whose registry, configuration and key all live in a temporary directory, so it can be driven by any Tentacle version.
        sealed class IsolatedTentacleInstance : IDisposable
        {
            const string Name = "Tentacle";
            readonly TemporaryDirectory root = new();

            public string DataDirectory => Path.Combine(root.DirectoryPath, "data");
            public string KeyFile => Path.Combine(DataDirectory, Name, "keys", ConfigurationEncryptionKey.KeyFileName);
            string ConfigurationFile => Path.Combine(root.DirectoryPath, "tentacle.config");

            public string EncryptedCertificate
                => XDocument.Load(ConfigurationFile).Root!.Elements("set").Single(e => (string?)e.Attribute("key") == "Tentacle.Certificate").Value;

            public async Task CreateWithACertificate(string tentacle)
            {
                await Run(tentacle, "create-instance", "--instance", Name, "--config", ConfigurationFile);
                await Run(tentacle, "new-certificate", "--instance", Name);
            }

            public async Task<string> Thumbprint(string tentacle)
                => (await Run(tentacle, "show-thumbprint", "--instance", Name)).StandardOutput.Trim();

            public Task UpgradeConfigurationEncryption(string tentacle)
                => Run(tentacle, "upgrade-configuration-encryption", "--instance", Name);

            public async Task<(int ExitCode, string Output)> TryUpgradeConfigurationEncryption(string tentacle)
            {
                var result = await Run(tentacle, CommandResultValidation.None, "upgrade-configuration-encryption", "--instance", Name);
                return (result.ExitCode, result.StandardOutput + result.StandardError);
            }

            public void PreventTheKeyDirectoryFromBeingCreated()
                => File.WriteAllText(DataDirectory, "a file where the data directory should be");

            Task<BufferedCommandResult> Run(string tentacle, params string[] arguments)
                => Run(tentacle, CommandResultValidation.ZeroExitCode, arguments);

            Task<BufferedCommandResult> Run(string tentacle, CommandResultValidation validation, params string[] arguments)
                => Cli.Wrap(tentacle)
                    .WithArguments(arguments)
                    .WithValidation(validation)
                    .WithEnvironmentVariables(e => e
                        .Set(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, Path.Combine(root.DirectoryPath, "home"))
                        .Set(EnvironmentVariables.TentacleInstancesDataDirectory, DataDirectory))
                    .ExecuteBufferedAsync();

            public void Dispose() => root.Dispose();
        }
    }
}
#endif
