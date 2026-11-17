#if NET
using System;
using System.IO;
using System.Linq;
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
    [TestFixture]
    [LinuxTest]
    public class TentacleConfigurationUpgradeFixture
    {
        const string Certificate = "MIIC...the certificate...";

        string directory;
        string configurationFile;
        ISystemLog log;
        OctopusPhysicalFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "octopus-config-upgrade-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            configurationFile = Path.Combine(directory, "tentacle.config");
            log = Substitute.For<ISystemLog>();
            fileSystem = new OctopusPhysicalFileSystem(log);
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, recursive: true);
        }

        [Test]
        public void WhenTheKeyIsMissingThenItIsCreatedAndLegacyValuesAreReEncrypted()
        {
            // Arrange
            var keyFilePath = Path.Combine(directory, "keys", "Tentacle", ConfigurationEncryptionKey.KeyFileName);
            var store = CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            RawCertificate().Should().NotStartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix, "without a key, values are written the way earlier versions wrote them");

            // Act
            CreateUpgrade(store, keyFilePath).Run();

            // Assert
            File.Exists(keyFilePath).Should().BeTrue();
            RawCertificate().Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            CreateStore(keyFilePath).Get<string>(TentacleConfiguration.CertificateSettingName, protectionLevel: ProtectionLevel.MachineKey).Should().Be(Certificate);
            log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void WhenOnlyTheKeyIsPreparedThenItIsCreatedAndValuesStayLegacy()
        {
            // Arrange
            var keyFilePath = Path.Combine(directory, "keys", "Tentacle", ConfigurationEncryptionKey.KeyFileName);
            var store = CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = RawCertificate();

            // Act
            CreateUpgrade(store, keyFilePath).PrepareKey();

            // Assert
            File.Exists(keyFilePath).Should().BeTrue();
            RawCertificate().Should().Be(legacyValue);
            log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void WhenTheKeyCannotBeCreatedThenRunFailsAndValuesStayLegacy()
        {
            // Arrange
            var keyFilePath = Path.Combine(configurationFile, "not-a-directory", ConfigurationEncryptionKey.KeyFileName);
            var store = CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = RawCertificate();

            // Act
            Action run = () => CreateUpgrade(store, keyFilePath).Run();

            // Assert
            run.Should().Throw<Exception>();
            RawCertificate().Should().Be(legacyValue);
        }

        [Test]
        public void WhenTheConfigurationIsNotInTheRegistryThenNoKeyIsCreatedAndValuesStayLegacy()
        {
            // Arrange
            var keyFilePath = Path.Combine(directory, "keys", "Tentacle", ConfigurationEncryptionKey.KeyFileName);
            var store = CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = RawCertificate();

            // Act
            CreateUpgrade(store, keyFilePath: null, instanceName: null).Run();

            // Assert
            File.Exists(keyFilePath).Should().BeFalse();
            RawCertificate().Should().Be(legacyValue);
            log.Received(1).Warn(Arg.Any<string>());
        }

        [Test]
        public void UpgradeConfigurationEncryptionCreatesTheKeyAndReEncryptsLegacyValues()
        {
            // Arrange
            var keyFilePath = Path.Combine(directory, "keys", "Tentacle", ConfigurationEncryptionKey.KeyFileName);
            var store = CreateStore(keyFilePath);
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);

            // Act
            StartUpgradeCommand(store, keyFilePath);

            // Assert
            File.Exists(keyFilePath).Should().BeTrue();
            RawCertificate().Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            CreateStore(keyFilePath).Get<string>(TentacleConfiguration.CertificateSettingName, protectionLevel: ProtectionLevel.MachineKey).Should().Be(Certificate);
        }

        [Test]
        public void UpgradeConfigurationEncryptionFailsForAConfigurationNotInTheRegistry()
        {
            // Arrange
            var store = CreateStore(Path.Combine(directory, "keys", "Tentacle", ConfigurationEncryptionKey.KeyFileName));
            store.Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var legacyValue = RawCertificate();

            // Act
            Action start = () => StartUpgradeCommand(store, keyFilePath: null, instanceName: null);

            // Assert
            start.Should().Throw<ControlledFailureException>().WithMessage("*create-instance --instance*");
            RawCertificate().Should().Be(legacyValue);
        }

        [Test]
        public void AValueProtectedWithTheKeyIsNeverRewrittenWithALegacyKey()
        {
            // Arrange
            var keyFilePath = Path.Combine(directory, "my-key", ConfigurationEncryptionKey.KeyFileName);
            new ConfigurationEncryptionKey(log, fileSystem, keyFilePath).CreateIfMissing();
            CreateStore(keyFilePath).Set<string>(TentacleConfiguration.CertificateSettingName, Certificate, ProtectionLevel.MachineKey);
            var protectedValue = RawCertificate();
            var storeWithoutKey = CreateStore(Path.Combine(directory, "missing", ConfigurationEncryptionKey.KeyFileName));

            // Act
            Action set = () => storeWithoutKey.Set<string>(TentacleConfiguration.CertificateSettingName, "another certificate", ProtectionLevel.MachineKey);

            // Assert
            set.Should().Throw<CryptographicException>();
            RawCertificate().Should().Be(protectedValue);
        }

        readonly InMemoryCryptoKeyNixSource legacyKey = new();

        XmlFileKeyValueStore CreateStore(string keyFilePath)
            => new(fileSystem, configurationFile, encryptor: new LinuxMachineKeyEncryptor(log, new ConfigurationEncryptionKey(log, fileSystem, keyFilePath), new[] { legacyKey }));

        TentacleConfigurationUpgrade CreateUpgrade(XmlFileKeyValueStore store, string? keyFilePath, string? instanceName = "Tentacle")
            => new(CreateSelector(store, keyFilePath, instanceName), fileSystem, log);

        void StartUpgradeCommand(XmlFileKeyValueStore store, string? keyFilePath, string? instanceName = "Tentacle")
        {
            var selector = CreateSelector(store, keyFilePath, instanceName);
            var upgrade = new TentacleConfigurationUpgrade(selector, fileSystem, log);
            var command = new UpgradeConfigurationEncryptionCommand(new Lazy<ITentacleConfigurationUpgrade>(() => upgrade), selector, log, Substitute.For<ILogFileOnlyLogger>());
            command.Start(new string[0], Substitute.For<ICommandRuntime>(), new OptionSet());
        }

        IApplicationInstanceSelector CreateSelector(XmlFileKeyValueStore store, string? keyFilePath, string? instanceName)
        {
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(new ApplicationInstanceConfiguration(instanceName, configurationFile, store, store, keyFilePath));
            return selector;
        }

        string RawCertificate()
            => XDocument.Load(configurationFile).Root!.Elements("set").Single(e => (string?)e.Attribute("key") == TentacleConfiguration.CertificateSettingName).Value;
    }
}
#endif
