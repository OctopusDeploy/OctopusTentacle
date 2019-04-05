using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    class XmlFileKeyValueStoreFixture
    {
        [Test]
        public void WritesSortedXmlUsingCorrectTypes()
        {
            var configurationFile = System.IO.Path.GetTempFileName();
            var fileSystem = new OctopusPhysicalFileSystem();
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            
            var settings = new XmlFileKeyValueStore(fileSystem, configurationFile);
            settings.Set("group1.setting2", 123);
            settings.Set("group1.setting1", true);
            settings.Set<string>("group2.setting3", "a string");

            settings.Save();
            
            var fileContents = XDocument.Parse(fileSystem.ReadAllText(configurationFile));

            var expected = XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<octopus-settings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <set key=""group1.setting1"">True</set>
  <set key=""group1.setting2"">123</set>
  <set key=""group2.setting3"">a string</set>
</octopus-settings>");
            fileContents.Should().BeEquivalentTo(expected);
        }

        class RoundTripTests
        {
            private string configurationFile;
            private IOctopusFileSystem fileSystem;
            private XmlFileKeyValueStore reloadedSettings;

            [OneTimeSetUp]
            public void Setup()
            {
                configurationFile = System.IO.Path.GetTempFileName();
                fileSystem = new OctopusPhysicalFileSystem();
                fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
                var configurationObject = new MyObject
                {
                    IntField = 10, BooleanField = true, ArrayField = new[]
                    {
                        new MyNestedObject {Id = 1},
                        new MyNestedObject {Id = 2},
                        new MyNestedObject {Id = 3}
                    }
                };
            
                var settings = new XmlFileKeyValueStore(fileSystem, configurationFile);
                settings.Set("group1.setting2", 123);
                settings.Set("group1.setting1", true);
                settings.Set<string>("group2.setting3", "a string");
                settings.Set("group3.setting4", configurationObject);
                settings.Set<string>("group4.setting5", null);
                settings.Set<MyObject>("group4.setting6", null);
                settings.Set("group5.setting2", 123, ProtectionLevel.MachineKey);
                settings.Set("group5.setting1", true, ProtectionLevel.MachineKey);
                settings.Set<string>("group5.setting3", "a string", ProtectionLevel.MachineKey);
                settings.Set("group5.setting4", configurationObject, ProtectionLevel.MachineKey);
                settings.Set<string>("group5.setting5", null, ProtectionLevel.MachineKey);
                settings.Set<MyObject>("group5.setting6", null, ProtectionLevel.MachineKey);
                settings.Save();
                
                reloadedSettings = new XmlFileKeyValueStore(fileSystem, configurationFile);
            }

            [Test]
            public void ReadsBooleanValue()
            {
                reloadedSettings.Get("group1.setting1", false).Should().BeTrue();
            }
            
            [Test]
            public void ReadsIntValue()
            {
                reloadedSettings.Get("group1.setting2", 1).Should().Be(123);
            }
            
            [Test]
            public void ReadsStringValue()
            {
                reloadedSettings.Get("group2.setting3", "").Should().Be("a string");
            }
            
            [Test]
            public void ReadsNestedObjectValue()
            {
                var nestedObject = reloadedSettings.Get<MyObject>("group3.setting4", null);
                nestedObject.IntField.Should().Be(10);
                nestedObject.BooleanField.Should().BeTrue();
                nestedObject.ArrayField.Length.Should().Be(3);
                nestedObject.ArrayField[0].Id.Should().Be(1);
                nestedObject.ArrayField[1].Id.Should().Be(2);
                nestedObject.ArrayField[2].Id.Should().Be(3);
            }
            
            [Test]
            public void ReadsNullStringValue()
            {
                reloadedSettings.Get<string>("group4.setting5", null).Should().Be(null);
            }

            [Test]
            public void ReadsNullObjectValue()
            {
                reloadedSettings.Get<MyObject>("group4.setting6", null).Should().Be(null);
            }
            
            [Test]
            public void ReadsEncryptedBooleanValue()
            {
                reloadedSettings.Get("group5.setting1", false, ProtectionLevel.MachineKey).Should().BeTrue();
            }
            
            [Test]
            public void ReadsEncryptedIntValue()
            {
                reloadedSettings.Get("group5.setting2", 1, ProtectionLevel.MachineKey).Should().Be(123);
            }
            
            [Test]
            public void ReadsEncryptedStringValue()
            {
                reloadedSettings.Get("group5.setting3", "", ProtectionLevel.MachineKey).Should().Be("a string");
            }
            
            [Test]
            public void ReadsEncryptedNestedObjectValue()
            {
                var nestedObject = reloadedSettings.Get<MyObject>("group5.setting4", null, ProtectionLevel.MachineKey);
                nestedObject.IntField.Should().Be(10);
                nestedObject.BooleanField.Should().BeTrue();
                nestedObject.ArrayField.Length.Should().Be(3);
                nestedObject.ArrayField[0].Id.Should().Be(1);
                nestedObject.ArrayField[1].Id.Should().Be(2);
                nestedObject.ArrayField[2].Id.Should().Be(3);
            }
            
            [Test]
            public void ReadsEncryptedNullStringValue()
            {
                reloadedSettings.Get<string>("group5.setting5", null, ProtectionLevel.MachineKey).Should().Be(null);
            }

            [Test]
            public void ReadsEncryptedNullObjectValue()
            {
                reloadedSettings.Get<MyObject>("group5.setting6", null, ProtectionLevel.MachineKey).Should().Be(null);
            }
        }
    }
}
