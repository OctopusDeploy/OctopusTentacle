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
        const string InstanceName = "Tentacle";

        [Test]
        public async Task AVersion14ConfigurationEncryptedWithTheMachineIdIsUpgradedToItsOwnKey()
        {
            // Arrange
            using var root = new TemporaryDirectory();
            var dataDirectory = Path.Combine(root.DirectoryPath, "data");
            var configurationFile = Path.Combine(root.DirectoryPath, "tentacle.config");
            var environment = Environment(root, dataDirectory);
            var version14Tentacle = await GetVersion14Tentacle();
            await Run(version14Tentacle, environment, "create-instance", "--instance", InstanceName, "--config", configurationFile);
            await Run(version14Tentacle, environment, "new-certificate", "--instance", InstanceName);
            var thumbprint = (await Run(version14Tentacle, environment, "show-thumbprint", "--instance", InstanceName)).StandardOutput.Trim();
            RawCertificate(configurationFile).Should().NotStartWith(LinuxMachineKeyEncryptor.Specifier + "$");

            // Act
            await Run(TentacleExeFinder.FindTentacleExe(), environment, "upgrade-configuration-encryption", "--instance", InstanceName);

            // Assert
            RawCertificate(configurationFile).Should().StartWith(LinuxMachineKeyEncryptor.Specifier + "$");
            File.Exists(Path.Combine(dataDirectory, InstanceName, "keys", ConfigurationEncryptionKey.KeyFileName)).Should().BeTrue();
            var upgradedThumbprint = await Run(TentacleExeFinder.FindTentacleExe(), environment, "show-thumbprint", "--instance", InstanceName);
            upgradedThumbprint.StandardOutput.Trim().Should().Be(thumbprint);
        }

        [Test]
        public async Task WhenItsOwnKeyCannotBeCreatedThenTheUpgradeFailsAndAVersion14ConfigurationStaysEncryptedWithTheMachineId()
        {
            // Arrange
            using var root = new TemporaryDirectory();
            var dataDirectoryThatIsAFile = Path.Combine(root.DirectoryPath, "data");
            await File.WriteAllTextAsync(dataDirectoryThatIsAFile, "not a directory");
            var configurationFile = Path.Combine(root.DirectoryPath, "tentacle.config");
            var environment = Environment(root, dataDirectoryThatIsAFile);
            var version14Tentacle = await GetVersion14Tentacle();
            await Run(version14Tentacle, environment, "create-instance", "--instance", InstanceName, "--config", configurationFile);
            await Run(version14Tentacle, environment, "new-certificate", "--instance", InstanceName);
            var version14Certificate = RawCertificate(configurationFile);

            // Act
            var upgrade = await Run(TentacleExeFinder.FindTentacleExe(), environment, CommandResultValidation.None, "upgrade-configuration-encryption", "--instance", InstanceName);

            // Assert
            var output = upgrade.StandardOutput + upgrade.StandardError;
            upgrade.ExitCode.Should().NotBe(0, output);
            output.Should().Contain(dataDirectoryThatIsAFile);
            RawCertificate(configurationFile).Should().Be(version14Certificate);
        }

        Task<string> GetVersion14Tentacle()
            => TentacleFetcher.GetTentacleVersion(Path.GetTempPath(), TentacleVersions.v14_0_4849_LastWithMachineIdConfigurationEncryption, DefaultTentacleRuntime.Value, Logger, CancellationToken);

        static (string Name, string Value)[] Environment(TemporaryDirectory root, string dataDirectory)
            => new[]
            {
                (EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, Path.Combine(root.DirectoryPath, "home")),
                (EnvironmentVariables.TentacleInstancesDataDirectory, dataDirectory)
            };

        static string RawCertificate(string configurationFile)
            => XDocument.Load(configurationFile).Root!.Elements("set").Single(e => (string?)e.Attribute("key") == "Tentacle.Certificate").Value;

        static Task<BufferedCommandResult> Run(string tentacleExe, (string Name, string Value)[] environment, params string[] arguments)
            => Run(tentacleExe, environment, CommandResultValidation.ZeroExitCode, arguments);

        static Task<BufferedCommandResult> Run(string tentacleExe, (string Name, string Value)[] environment, CommandResultValidation validation, params string[] arguments)
            => Cli.Wrap(tentacleExe)
                .WithArguments(arguments)
                .WithValidation(validation)
                .WithEnvironmentVariables(environment.ToDictionary(x => x.Name, x => (string?)x.Value))
                .ExecuteBufferedAsync();
    }
}
#endif
