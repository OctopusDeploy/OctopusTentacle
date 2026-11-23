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
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
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
