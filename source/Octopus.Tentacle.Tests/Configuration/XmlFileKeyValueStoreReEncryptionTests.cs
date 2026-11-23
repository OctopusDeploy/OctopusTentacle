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
    public class XmlFileKeyValueStoreReEncryptionTests
    {
        [Test]
        public void WhenSerialisedValuesAreReEncryptedThenTypedReadsStillWork()
        {
            // Arrange
            var configurationFile = Path.GetTempFileName();
            var fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            var legacyKey = new InMemoryCryptoKeyNixSource();
            var encryptor = new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), InMemoryConfigurationEncryptionKey.Create(), new[] { legacyKey });
            var legacyStore = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: new LegacySchemeEncryptor(legacyKey));
            var store = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: encryptor);
            var names = new[] { "a.bool", "an.int", "an.enum", "an.object" };
            var configurationObject = new MyObject { IntField = 10, BooleanField = true, EnumField = SomeEnum.SomeOtherEnumValue, ArrayField = new[] { new MyNestedObject { Id = 1 } } };
            legacyStore.Set("a.bool", true, ProtectionLevel.MachineKey);
            legacyStore.Set("an.int", 123, ProtectionLevel.MachineKey);
            legacyStore.Set("an.enum", SomeEnum.SomeOtherEnumValue, ProtectionLevel.MachineKey);
            legacyStore.Set("an.object", configurationObject, ProtectionLevel.MachineKey);

            // Act
            var reEncrypted = names.Select(store.ReEncryptIfLegacy).ToArray();

            // Assert
            reEncrypted.Should().AllBeEquivalentTo(true);
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
