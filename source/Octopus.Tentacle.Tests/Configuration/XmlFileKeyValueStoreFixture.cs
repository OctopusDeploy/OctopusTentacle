using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    class MyNestedObject
    {
        public int Id { get; set; }
    }

    enum SomeEnum
    {
        SomeEnumValue,
        SomeOtherEnumValue
    }

    class MyObject
    {
        public bool BooleanField { get; set; }
        public int IntField { get; set; }
        public SomeEnum EnumField { get; set; }
        public MyNestedObject[] ArrayField { get; set; }
    }

    class XmlFileKeyValueStoreTests
    {
        static readonly KeyValueStoreSource[] AllSources = (KeyValueStoreSource[])Enum.GetValues(typeof(KeyValueStoreSource));
        static readonly KeyValueStoreSource[] CurrentImplementationSources = { KeyValueStoreSource.InMemory, KeyValueStoreSource.ReloadedFromFile };

        [Test]
        public void WritesSortedXmlUsingCorrectTypes()
        {
            var configurationFile = Path.GetTempFileName();
            var fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");

            var settings = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: TestMachineKeyEncryptor.Current);
            settings.Set("group1.setting2", 123);
            settings.Set("group1.setting1", true);
            settings.Set<string>("group2.setting3", "a string");

            settings.Save();

            var fileContents = XDocument.Parse(fileSystem.ReadAllText(configurationFile));

            //we want to write bools to the file as lowercase, so its backwards compatible

            var expected = XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<octopus-settings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <set key=""group1.setting1"">true</set>
  <set key=""group1.setting2"">123</set>
  <set key=""group2.setting3"">a string</set>
</octopus-settings>");
            fileContents.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void ReturnsNiceExceptionOnInvalidData()
        {
            var configurationFile = Path.GetTempFileName();
            var fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");

            var settings = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: TestMachineKeyEncryptor.Current);
            settings.Set<string>("Int.Setting", "NotAnInt");
            settings.Set<string>("Bool.Setting", "NotABool");
            settings.Set<string>("Encrypted.Int.Setting", "NotAnInt", ProtectionLevel.MachineKey);
            settings.Set<string>("Encrypted.Bool.Setting", "NotABool", ProtectionLevel.MachineKey);

            settings.Save();

            var reloadedSettings = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: TestMachineKeyEncryptor.Current);

            reloadedSettings.Invoking(x => x.Get("Int.Setting", 1))
                .Should()
                .Throw<FormatException>()
                .WithMessage("Unable to parse configuration key 'Int.Setting' as a 'Int32'. Value was 'NotAnInt'.");
            reloadedSettings.Invoking(x => x.Get("Bool.Setting", true))
                .Should()
                .Throw<FormatException>()
                .WithMessage("Unable to parse configuration key 'Bool.Setting' as a 'Boolean'. Value was 'NotABool'.");
            reloadedSettings.Invoking(x => x.Get("Encrypted.Int.Setting", 1, ProtectionLevel.MachineKey))
                .Should()
                .Throw<FormatException>()
                .WithMessage("Unable to parse configuration key 'Encrypted.Int.Setting' as a 'Int32'.");
            reloadedSettings.Invoking(x => x.Get("Encrypted.Bool.Setting", true, ProtectionLevel.MachineKey))
                .Should()
                .Throw<FormatException>()
                .WithMessage("Unable to parse configuration key 'Encrypted.Bool.Setting' as a 'Boolean'.");
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsBooleanValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get("group1.setting1", false).Should().BeTrue();
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsIntValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get("group1.setting2", 1).Should().Be(123);
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsStringValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get("group2.setting3", "").Should().Be("a string");
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsNestedObjectValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            var nestedObject = reloadedSettings.Get<MyObject>("group3.setting4");
            nestedObject.IntField.Should().Be(10);
            nestedObject.BooleanField.Should().BeTrue();
            nestedObject.EnumField.Should().Be(SomeEnum.SomeOtherEnumValue);
            nestedObject.ArrayField.Length.Should().Be(3);
            nestedObject.ArrayField[0].Id.Should().Be(1);
            nestedObject.ArrayField[1].Id.Should().Be(2);
            nestedObject.ArrayField[2].Id.Should().Be(3);
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsNullStringValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get<string>("group4.setting5").Should().Be(null);
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsNullObjectValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get<MyObject>("group4.setting6").Should().Be(null);
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsEncryptedBooleanValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get("group5.setting1", false, ProtectionLevel.MachineKey).Should().BeTrue();
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsEncryptedIntValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get("group5.setting2", 1, ProtectionLevel.MachineKey).Should().Be(123);
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsEncryptedStringValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get("group5.setting3", "", ProtectionLevel.MachineKey).Should().Be("a string");
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsEncryptedNestedObjectValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            var nestedObject = reloadedSettings.Get<MyObject>("group5.setting4", null, ProtectionLevel.MachineKey);
            nestedObject.IntField.Should().Be(10);
            nestedObject.BooleanField.Should().BeTrue();
            nestedObject.EnumField.Should().Be(SomeEnum.SomeOtherEnumValue);
            nestedObject.ArrayField.Length.Should().Be(3);
            nestedObject.ArrayField[0].Id.Should().Be(1);
            nestedObject.ArrayField[1].Id.Should().Be(2);
            nestedObject.ArrayField[2].Id.Should().Be(3);
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsEncryptedNullStringValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get<string>("group5.setting5", null, ProtectionLevel.MachineKey).Should().Be(null);
        }

        [TestCaseSource(nameof(AllSources))]
        public void ReadsEncryptedNullObjectValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get<MyObject>("group5.setting6", null, ProtectionLevel.MachineKey).Should().Be(null);
        }

        [TestCaseSource(nameof(CurrentImplementationSources))]
        public void ReadsEncryptedByteArrayValue(KeyValueStoreSource source)
        {
            var encryptedValue = RandomBytes();
            var reloadedSettings = StoreWithAllSettings(source, encryptedValue);

            reloadedSettings.Get<byte[]>("secretthing").Should().BeEquivalentTo(encryptedValue);
        }

        [TestCaseSource(nameof(CurrentImplementationSources))]
        public void ReadsMachineEncryptedByteArrayValue(KeyValueStoreSource source)
        {
            var encryptedValue = RandomBytes();
            var reloadedSettings = StoreWithAllSettings(source, encryptedValue);

            reloadedSettings.Get<byte[]>("secretmachinething", null, ProtectionLevel.MachineKey).Should().BeEquivalentTo(encryptedValue);
        }

        [TestCaseSource(nameof(CurrentImplementationSources))]
        public void ReadsEnumValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get<SomeEnum>("group4.setting7").Should().Be(SomeEnum.SomeOtherEnumValue);
        }

        [TestCaseSource(nameof(CurrentImplementationSources))]
        public void ReadsNullableEnumValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get<SomeEnum?>("group4.setting8").Should().BeNull();
        }

        [TestCaseSource(nameof(CurrentImplementationSources))]
        public void ReadsEncryptedEnumValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get("group5.setting7", SomeEnum.SomeEnumValue, ProtectionLevel.MachineKey).Should().Be(SomeEnum.SomeOtherEnumValue);
        }

        [TestCaseSource(nameof(CurrentImplementationSources))]
        public void ReadsEncryptedNullableEnumValue(KeyValueStoreSource source)
        {
            var reloadedSettings = StoreWithAllSettings(source);

            reloadedSettings.Get<SomeEnum?>("group5.setting8", null, ProtectionLevel.MachineKey).Should().BeNull();
        }

        static IKeyValueStore StoreWithAllSettings(KeyValueStoreSource source, byte[] encryptedValue = null)
        {
            var configurationObject = new MyObject
            {
                IntField = 10,
                BooleanField = true,
                EnumField = SomeEnum.SomeOtherEnumValue,
                ArrayField = new[]
                {
                    new MyNestedObject { Id = 1 },
                    new MyNestedObject { Id = 2 },
                    new MyNestedObject { Id = 3 }
                }
            };

            var builder = new KeyValueStoreBuilder()
                .From(source)
                .With("group1.setting2", 123)
                .With("group1.setting1", true)
                .With<string>("group2.setting3", "a string")
                .With("group3.setting4", configurationObject)
                .With<string>("group4.setting5", null)
                .With<MyObject>("group4.setting6", null)
                .With("group4.setting7", SomeEnum.SomeOtherEnumValue)
                .With("group5.setting2", 123, ProtectionLevel.MachineKey)
                .With("group5.setting1", true, ProtectionLevel.MachineKey)
                .With<string>("group5.setting3", "a string", ProtectionLevel.MachineKey)
                .With("group5.setting4", configurationObject, ProtectionLevel.MachineKey)
                .With<string>("group5.setting5", null, ProtectionLevel.MachineKey)
                .With<MyObject>("group5.setting6", null, ProtectionLevel.MachineKey);

            // The old implementation did not support these types.
            if (source != KeyValueStoreSource.WrittenByOldImplementation)
            {
                builder
                    .With<SomeEnum?>("group4.setting8", null)
                    .With("group5.setting7", SomeEnum.SomeOtherEnumValue, ProtectionLevel.MachineKey)
                    .With<SomeEnum?>("group5.setting8", null, ProtectionLevel.MachineKey);
            }

            if (encryptedValue != null)
            {
                builder
                    .With("secretthing", encryptedValue)
                    .With("secretmachinething", encryptedValue, ProtectionLevel.MachineKey);
            }

            return builder.Build();
        }

        static byte[] RandomBytes()
        {
            var bytes = new byte[16];
            using var random = RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            return bytes;
        }
    }
}
