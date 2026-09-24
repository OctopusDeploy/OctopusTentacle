using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
    /// <summary>
    /// <see cref="IReEncryptingKeyValueStore.ReEncryptIfLegacy"/> on the store behind tentacle.config. The encryptor is
    /// built from in-memory keys so this runs the same way on every platform.
    /// </summary>
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
        }

        [Test]
        public void ALegacyStringIsReEncryptedInPlaceAndReadsBackUnchanged()
        {
            legacyStore.Set<string>("Tentacle.Certificate", "MIIC...the certificate...", ProtectionLevel.MachineKey);

            store.ReEncryptIfLegacy("Tentacle.Certificate").Should().BeTrue();

            RawValue("Tentacle.Certificate").Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            store.Get<string>("Tentacle.Certificate", protectionLevel: ProtectionLevel.MachineKey).Should().Be("MIIC...the certificate...");
            new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor).Get<string>("Tentacle.Certificate", protectionLevel: ProtectionLevel.MachineKey)
                .Should().Be("MIIC...the certificate...", "it was saved, not just changed in memory");
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
                store.ReEncryptIfLegacy(name).Should().BeTrue(name);

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

        [Test]
        public void AValueAlreadyInTheCurrentFormatIsLeftAlone()
        {
            store.Set<string>("Tentacle.Certificate", "the certificate", ProtectionLevel.MachineKey);
            var before = File.ReadAllText(configurationFile);

            store.ReEncryptIfLegacy("Tentacle.Certificate").Should().BeFalse();

            File.ReadAllText(configurationFile).Should().Be(before, "a rewrite would have used a fresh IV and changed the file");
        }

        [Test]
        public void AMissingOrBlankValueIsLeftAlone()
        {
            store.Set<string>("blank", " ", ProtectionLevel.MachineKey);
            var before = File.ReadAllText(configurationFile);

            store.ReEncryptIfLegacy("missing").Should().BeFalse();
            store.ReEncryptIfLegacy("blank").Should().BeFalse();

            File.ReadAllText(configurationFile).Should().Be(before);
        }

        [Test]
        public void UnprotectedValuesAreNeverTouched()
        {
            // An unprotected value is stored as plain text, which by definition has no prefix; re-encrypting it would destroy it.
            store.Set("Tentacle.Services.PortNumber", 10933);
            store.Set<string>("Tentacle.Deployment.ApplicationDirectory", "/home/Octopus/Applications");

            // Only the caller knows which settings are protected, so this is what ProtectedSettingsMigrator relies on:
            // it only ever names settings written with ProtectionLevel.MachineKey.
            ProtectedSettingsMigrator.ProtectedSettingNames.Should().BeEquivalentTo(
                "Tentacle.Certificate", "Octopus.Proxy.ProxyPassword", "Octopus.Server.Proxy.ProxyPassword");
            ProtectedSettingsMigrator.ProtectedSettingNames.Should().NotContain("Tentacle.Services.PortNumber");
        }

        [Test]
        public void WhenTheLegacyValueCannotBeDecrypted_ThenThrowsAndLeavesTheFileUntouched()
        {
            var someOtherMachine = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: new LegacySchemeEncryptor(new InMemoryCryptoKeyNixSource()));
            someOtherMachine.Set<string>("Tentacle.Certificate", "the certificate", ProtectionLevel.MachineKey);
            var before = File.ReadAllText(configurationFile);

            store.Invoking(x => x.ReEncryptIfLegacy("Tentacle.Certificate")).Should().Throw<AggregateException>();

            File.ReadAllText(configurationFile).Should().Be(before);
        }

        string RawValue(string name)
            => XDocument.Load(configurationFile).Root!.Elements("set").Single(e => (string)e.Attribute("key") == name).Value;
    }
}
