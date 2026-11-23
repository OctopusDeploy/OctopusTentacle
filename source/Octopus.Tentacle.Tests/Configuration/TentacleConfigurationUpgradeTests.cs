#if NET
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Xml.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [LinuxOrMacTest]
    [UnsupportedOSPlatform("windows")]
    public class TentacleConfigurationUpgradeTests
    {
        const string Certificate = "MIIC...the certificate...";

        [Test]
        public void WhenTheKeyIsMissingThenItIsCreatedAndLegacyValuesAreReEncrypted()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var keyFilePath = sandbox.DefaultKeyFilePath;
            var store = sandbox.CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            sandbox.RawCertificate().Should().NotStartWith(LinuxMachineKeyEncryptor.Specifier + "$", "without a key, values are written the way earlier versions wrote them");

            // Act
            sandbox.CreateUpgrade(store, keyFilePath).Upgrade();

            // Assert
            File.Exists(keyFilePath).Should().BeTrue();
            sandbox.RawCertificate().Should().StartWith(LinuxMachineKeyEncryptor.Specifier + "$");
            sandbox.CreateStore(keyFilePath).Get<string>(TentacleConfiguration.CertificateSettingName, protectionLevel: ProtectionLevel.MachineKey).Should().Be(Certificate);
            sandbox.Log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void WhenOnlyTheKeyIsPreparedThenItIsCreatedAndValuesStayLegacy()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var keyFilePath = sandbox.DefaultKeyFilePath;
            var store = sandbox.CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = sandbox.RawCertificate();

            // Act
            sandbox.CreateUpgrade(store, keyFilePath).PrepareKey();

            // Assert
            File.Exists(keyFilePath).Should().BeTrue();
            sandbox.RawCertificate().Should().Be(legacyValue);
            sandbox.Log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void WhenTheKeyCannotBeCreatedThenRunFailsAndValuesStayLegacy()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var keyFilePath = Path.Combine(sandbox.ConfigurationFile, "not-a-directory", ConfigurationEncryptionKey.KeyFileName);
            var store = sandbox.CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = sandbox.RawCertificate();

            // Act
            Action run = () => sandbox.CreateUpgrade(store, keyFilePath).Upgrade();

            // Assert
            run.Should().Throw<Exception>();
            sandbox.RawCertificate().Should().Be(legacyValue);
        }

        [Test]
        public void WhenTheConfigurationIsNotInTheRegistryThenNoKeyIsCreatedAndValuesStayLegacy()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var keyFilePath = sandbox.DefaultKeyFilePath;
            var store = sandbox.CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = sandbox.RawCertificate();

            // Act
            sandbox.CreateUpgrade(store, keyFilePath: null, instanceName: null).Upgrade();

            // Assert
            File.Exists(keyFilePath).Should().BeFalse();
            sandbox.RawCertificate().Should().Be(legacyValue);
            sandbox.Log.Received(1).Warn(Arg.Any<string>());
        }

        [Test]
        public void UpgradeConfigurationEncryptionCreatesTheKeyAndReEncryptsLegacyValues()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var keyFilePath = sandbox.DefaultKeyFilePath;
            var store = sandbox.CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);

            // Act
            sandbox.StartUpgradeCommand(store, keyFilePath);

            // Assert
            File.Exists(keyFilePath).Should().BeTrue();
            sandbox.RawCertificate().Should().StartWith(LinuxMachineKeyEncryptor.Specifier + "$");
            sandbox.CreateStore(keyFilePath).Get<string>(TentacleConfiguration.CertificateSettingName, protectionLevel: ProtectionLevel.MachineKey).Should().Be(Certificate);
        }

        [Test]
        public void UpgradeConfigurationEncryptionFailsForAConfigurationNotInTheRegistry()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var store = sandbox.CreateStore(sandbox.DefaultKeyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = sandbox.RawCertificate();

            // Act
            Action start = () => sandbox.StartUpgradeCommand(store, keyFilePath: null, instanceName: null);

            // Assert
            start.Should().Throw<ControlledFailureException>().WithMessage("*create-instance --instance*");
            sandbox.RawCertificate().Should().Be(legacyValue);
        }

        [Test]
        public void AValueProtectedWithTheKeyIsNeverRewrittenWithALegacyKey()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var keyFilePath = Path.Combine(sandbox.Directory, "my-key", ConfigurationEncryptionKey.KeyFileName);
            new ConfigurationEncryptionKey(sandbox.Log, sandbox.FileSystem, keyFilePath).CreateIfMissing();
            sandbox.CreateStore(keyFilePath).Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var protectedValue = sandbox.RawCertificate();
            var storeWithoutKey = sandbox.CreateStore(Path.Combine(sandbox.Directory, "missing", ConfigurationEncryptionKey.KeyFileName));

            // Act
            Action set = () => storeWithoutKey.Set<string>(TentacleConfiguration.CertificateSettingName, "another certificate", ProtectionLevel.MachineKey);

            // Assert
            set.Should().Throw<CryptographicException>();
            sandbox.RawCertificate().Should().Be(protectedValue);
        }

        [Test]
        public void WhenTheKeyIsLostAfterMigrationThenStartLeavesProtectedValuesUntouched()
        {
            // Arrange
            using var sandbox = new ConfigurationSandbox();
            var keyFilePath = sandbox.DefaultKeyFilePath;
            var store = sandbox.CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            sandbox.CreateUpgrade(store, keyFilePath).Upgrade();
            var protectedValue = sandbox.RawCertificate();
            File.Delete(keyFilePath);

            // Act
            sandbox.CreateUpgrade(sandbox.CreateStore(keyFilePath), keyFilePath).Upgrade();

            // Assert
            sandbox.RawCertificate().Should().Be(protectedValue);
            sandbox.RawCertificate().Should().StartWith(LinuxMachineKeyEncryptor.Specifier + "$");
        }

        sealed class ConfigurationSandbox : IDisposable
        {
            readonly InMemoryCryptoKeyNixSource legacyKey = new();

            public ConfigurationSandbox()
            {
                Directory = Path.Combine(Path.GetTempPath(), "octopus-config-upgrade-" + Guid.NewGuid());
                System.IO.Directory.CreateDirectory(Directory);
                ConfigurationFile = Path.Combine(Directory, "tentacle.config");
                FileSystem = new OctopusPhysicalFileSystem(Log);
                FileSystem.OverwriteFile(ConfigurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            }

            public string Directory { get; }
            public string ConfigurationFile { get; }
            public ISystemLog Log { get; } = Substitute.For<ISystemLog>();
            public OctopusPhysicalFileSystem FileSystem { get; }

            public string DefaultKeyFilePath => Path.Combine(Directory, "keys", "Tentacle", ConfigurationEncryptionKey.KeyFileName);

            public XmlFileKeyValueStore CreateStore(string keyFilePath)
                => new(FileSystem, ConfigurationFile, encryptor: new LinuxMachineKeyEncryptor(Log, new ConfigurationEncryptionKey(Log, FileSystem, keyFilePath), new[] { legacyKey }));

            public TentacleConfigurationUpgrade CreateUpgrade(XmlFileKeyValueStore store, string? keyFilePath, string? instanceName = "Tentacle")
                => new(CreateSelector(store, keyFilePath, instanceName), FileSystem, Log);

            public void StartUpgradeCommand(XmlFileKeyValueStore store, string? keyFilePath, string? instanceName = "Tentacle")
            {
                var selector = CreateSelector(store, keyFilePath, instanceName);
                var upgrade = new TentacleConfigurationUpgrade(selector, FileSystem, Log);
                var command = new UpgradeConfigurationEncryptionCommand(new Lazy<ITentacleConfigurationUpgrade>(() => upgrade), selector, Log, Substitute.For<ILogFileOnlyLogger>());
                command.Start(new string[0], Substitute.For<ICommandRuntime>(), new OptionSet());
            }

            public string RawCertificate()
                => XDocument.Load(ConfigurationFile).Root!.Elements("set").Single(e => (string?)e.Attribute("key") == TentacleConfiguration.CertificateSettingName).Value;

            IApplicationInstanceSelector CreateSelector(XmlFileKeyValueStore store, string? keyFilePath, string? instanceName)
            {
                var selector = Substitute.For<IApplicationInstanceSelector>();
                selector.Current.Returns(new ApplicationInstanceConfiguration(instanceName, ConfigurationFile, store, store, keyFilePath));
                return selector;
            }

            public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
#endif
