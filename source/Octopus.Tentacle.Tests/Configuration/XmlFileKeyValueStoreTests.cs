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

    [TestFixture]
    class XmlFileKeyValueStoreTests
    {
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
            var reloadedSettings = new KeyValueStoreBuilder()
                .From(KeyValueStoreSource.ReloadedFromFile)
                .With<string>("Int.Setting", "NotAnInt")
                .With<string>("Bool.Setting", "NotABool")
                .With<string>("Encrypted.Int.Setting", "NotAnInt", ProtectionLevel.MachineKey)
                .With<string>("Encrypted.Bool.Setting", "NotABool", ProtectionLevel.MachineKey)
                .Build();

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

        [Test]
        public void ReadsBooleanValue([Values] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var store = new KeyValueStoreBuilder().From(source).With("setting", true, protectionLevel).Build();

            store.Get("setting", false, protectionLevel).Should().BeTrue();
        }

        [Test]
        public void ReadsIntValue([Values] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var store = new KeyValueStoreBuilder().From(source).With("setting", 123, protectionLevel).Build();

            store.Get("setting", 1, protectionLevel).Should().Be(123);
        }

        [Test]
        public void ReadsStringValue([Values] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var store = new KeyValueStoreBuilder().From(source).With<string>("setting", "a string", protectionLevel).Build();

            store.Get("setting", "", protectionLevel).Should().Be("a string");
        }

        [Test]
        public void ReadsNestedObjectValue([Values] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var value = new MyObject
            {
                IntField = 10,
                BooleanField = true,
                EnumField = SomeEnum.SomeOtherEnumValue,
                ArrayField = new[] { new MyNestedObject { Id = 1 }, new MyNestedObject { Id = 2 }, new MyNestedObject { Id = 3 } }
            };
            var store = new KeyValueStoreBuilder().From(source).With("setting", value, protectionLevel).Build();

            store.Get<MyObject>("setting", null, protectionLevel).Should().BeEquivalentTo(value);
        }

        [Test]
        public void ReadsNullStringValue([Values] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var store = new KeyValueStoreBuilder().From(source).With<string>("setting", null, protectionLevel).Build();

            store.Get<string>("setting", null, protectionLevel).Should().BeNull();
        }

        [Test]
        public void ReadsNullObjectValue([Values] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var store = new KeyValueStoreBuilder().From(source).With<MyObject>("setting", null, protectionLevel).Build();

            store.Get<MyObject>("setting", null, protectionLevel).Should().BeNull();
        }

        // The old implementation did not support enums or byte arrays.
        [Test]
        public void ReadsEnumValue([Values(KeyValueStoreSource.InMemory, KeyValueStoreSource.ReloadedFromFile)] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var store = new KeyValueStoreBuilder().From(source).With("setting", SomeEnum.SomeOtherEnumValue, protectionLevel).Build();

            store.Get("setting", SomeEnum.SomeEnumValue, protectionLevel).Should().Be(SomeEnum.SomeOtherEnumValue);
        }

        [Test]
        public void ReadsNullableEnumValue([Values(KeyValueStoreSource.InMemory, KeyValueStoreSource.ReloadedFromFile)] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var store = new KeyValueStoreBuilder().From(source).With<SomeEnum?>("setting", null, protectionLevel).Build();

            store.Get<SomeEnum?>("setting", null, protectionLevel).Should().BeNull();
        }

        [Test]
        public void ReadsByteArrayValue([Values(KeyValueStoreSource.InMemory, KeyValueStoreSource.ReloadedFromFile)] KeyValueStoreSource source, [Values] ProtectionLevel protectionLevel)
        {
            var value = new byte[16];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(value);
            var store = new KeyValueStoreBuilder().From(source).With("setting", value, protectionLevel).Build();

            store.Get<byte[]>("setting", null, protectionLevel).Should().BeEquivalentTo(value);
        }
    }
}
