#if !NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Configuration.Crypto;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class XmlFileKeyValueStoreReEncryptionFixture
    {
        string configurationFile;
        OctopusPhysicalFileSystem fileSystem;
        InMemoryCryptoKeyNixSource legacyKey;
        LinuxMachineKeyEncryptor encryptor;
        XmlFileKeyValueStore legacyStore;
        XmlFileKeyValueStore store;

        [SetUp]
        public void SetUp()
        {
            configurationFile = Path.GetTempFileName();
            fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");

            legacyKey = new InMemoryCryptoKeyNixSource();
            encryptor = new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), new InMemoryCryptoKeyNixSource(), new[] { legacyKey });

            // Writes what an earlier version would have left in the file...
            legacyStore = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: new LegacySchemeEncryptor(legacyKey));
            // ...and this is the store the upgraded Tentacle opens it with.
            store = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor);
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(configurationFile);
            File.Delete(BackupFile);
        }

        string BackupFile => configurationFile + XmlFileKeyValueStore.PreReEncryptionBackupSuffix;

        [Test]
        public void TheFirstReEncryptionKeepsACopyOfTheFileExactlyAsTheEarlierVersionLeftIt()
        {
            legacyStore.Set<string>("Tentacle.Certificate", "the certificate", ProtectionLevel.MachineKey);
            legacyStore.Set<string>("Octopus.Proxy.ProxyPassword", "the password", ProtectionLevel.MachineKey);
            var asTheEarlierVersionLeftIt = File.ReadAllBytes(configurationFile);

            store.ReEncryptIfLegacy("Tentacle.Certificate").Should().BeTrue();
            store.ReEncryptIfLegacy("Octopus.Proxy.ProxyPassword").Should().BeTrue();

            File.ReadAllBytes(BackupFile).Should().Equal(asTheEarlierVersionLeftIt, "the copy is byte for byte what was there before anything was rewritten, byte-order mark included");
            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(BackupFile).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite, "it still holds the certificate, under the old, weaker scheme");
            }
            new XmlFileKeyValueStore(fileSystem, BackupFile, encryptor: new LegacySchemeEncryptor(legacyKey))
                .Get<string>("Tentacle.Certificate", protectionLevel: ProtectionLevel.MachineKey)
                .Should().Be("the certificate", "an earlier version can read the copy");
        }

        [Test]
        public void AfterADowngradeAndAnotherUpgradeTheBackupIsReplaced()
        {
            legacyStore.Set<string>("Tentacle.Certificate", "the first certificate", ProtectionLevel.MachineKey);
            store.ReEncryptIfLegacy("Tentacle.Certificate");

            // The downgraded version restores its backup and then changes the configuration.
            File.Copy(BackupFile, configurationFile, overwrite: true);
            var downgraded = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: new LegacySchemeEncryptor(legacyKey));
            downgraded.Set<string>("Tentacle.Certificate", "the second certificate", ProtectionLevel.MachineKey);
            var asTheDowngradedVersionLeftIt = File.ReadAllBytes(configurationFile);

            new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor).ReEncryptIfLegacy("Tentacle.Certificate").Should().BeTrue();

            File.ReadAllBytes(BackupFile).Should().Equal(asTheDowngradedVersionLeftIt);
        }

        [Test]
        public void APartlyReEncryptedConfigurationNeverReplacesTheBackup()
        {
            legacyStore.Set<string>("Tentacle.Certificate", "the certificate", ProtectionLevel.MachineKey);
            legacyStore.Set<string>("Octopus.Proxy.ProxyPassword", "the password", ProtectionLevel.MachineKey);
            store.ReEncryptIfLegacy("Tentacle.Certificate");
            var firstBackup = File.ReadAllBytes(BackupFile);

            new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor).ReEncryptIfLegacy("Octopus.Proxy.ProxyPassword").Should().BeTrue();

            File.ReadAllBytes(BackupFile).Should().Equal(firstBackup);
        }

        [Test]
        public void SerialisedValuesKeepTheirSerialisedFormSoTypedReadsStillWork()
        {
            var configurationObject = new MyObject { IntField = 10, BooleanField = true, EnumField = SomeEnum.SomeOtherEnumValue, ArrayField = new[] { new MyNestedObject { Id = 1 } } };
            legacyStore.Set("a.bool", true, ProtectionLevel.MachineKey);
            legacyStore.Set("an.int", 123, ProtectionLevel.MachineKey);
            legacyStore.Set("an.enum", SomeEnum.SomeOtherEnumValue, ProtectionLevel.MachineKey);
            legacyStore.Set("an.object", configurationObject, ProtectionLevel.MachineKey);

            foreach (var name in new[] { "a.bool", "an.int", "an.enum", "an.object" })
            {
                store.ReEncryptIfLegacy(name).Should().BeTrue(name);
            }

            var reloaded = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor);
            reloaded.Get("a.bool", false, ProtectionLevel.MachineKey).Should().BeTrue();
            reloaded.Get("an.int", 0, ProtectionLevel.MachineKey).Should().Be(123);
            reloaded.Get("an.enum", SomeEnum.SomeEnumValue, ProtectionLevel.MachineKey).Should().Be(SomeEnum.SomeOtherEnumValue);
            var nested = reloaded.Get<MyObject>("an.object", null, ProtectionLevel.MachineKey);
            nested.IntField.Should().Be(10);
            nested.BooleanField.Should().BeTrue();
            nested.EnumField.Should().Be(SomeEnum.SomeOtherEnumValue);
            nested.ArrayField.Single().Id.Should().Be(1);
        }
    }
}
#endif
