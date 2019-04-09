using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using FluentAssertions;
using Newtonsoft.Json;
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
            var configurationFile = Path.GetTempFileName();
            var fileSystem = new OctopusPhysicalFileSystem();
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            
            var settings = new XmlFileKeyValueStore(fileSystem, configurationFile);
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
            var configurationFile = System.IO.Path.GetTempFileName();
            var fileSystem = new OctopusPhysicalFileSystem();
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            
            var settings = new XmlFileKeyValueStore(fileSystem, configurationFile);
            settings.Set<string>("Int.Setting", "NotAnInt");
            settings.Set<string>("Bool.Setting", "NotABool");
            settings.Set<string>("Encrypted.Int.Setting", "NotAnInt", ProtectionLevel.MachineKey);
            settings.Set<string>("Encrypted.Bool.Setting", "NotABool", ProtectionLevel.MachineKey);

            settings.Save();
            
            var reloadedSettings = new XmlFileKeyValueStore(fileSystem, configurationFile);

            reloadedSettings.Invoking(x => x.Get("Int.Setting", 1))
                .ShouldThrow<FormatException>()
                .WithMessage("Unable to parse configuration key 'Int.Setting' as a 'Int32'. Value was 'NotAnInt'.");
            reloadedSettings.Invoking(x => x.Get("Bool.Setting", true))
                .ShouldThrow<FormatException>()
                .WithMessage("Unable to parse configuration key 'Bool.Setting' as a 'Boolean'. Value was 'NotABool'.");
            reloadedSettings.Invoking(x => x.Get("Encrypted.Int.Setting", 1, ProtectionLevel.MachineKey))
                .ShouldThrow<FormatException>()
                .WithMessage("Unable to parse configuration key 'Encrypted.Int.Setting' as a 'Int32'.");
            reloadedSettings.Invoking(x => x.Get("Encrypted.Bool.Setting", true, ProtectionLevel.MachineKey))
                .ShouldThrow<FormatException>()
                .WithMessage("Unable to parse configuration key 'Encrypted.Bool.Setting' as a 'Boolean'.");
        }

        abstract class RoundTripTestBaseFixture
        {
            protected abstract XmlFileKeyValueStore SetupDataAndReloadKeyValueStore();

            private XmlFileKeyValueStore reloadedSettings;

            [OneTimeSetUp]
            public void Setup()
            {
                reloadedSettings = SetupDataAndReloadKeyValueStore();
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

        /// <summary>
        /// Tests to make sure we can read a file written by the old implementation
        /// where it json serialized a lot of things
        /// </summary>
        [TestFixture]
        class BackwardsCompatFixture : RoundTripTestBaseFixture
        {
            protected override XmlFileKeyValueStore SetupDataAndReloadKeyValueStore()
            {
                var configurationFile = Path.GetTempFileName();
                var fileSystem = new OctopusPhysicalFileSystem();
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
            
                var settings = new OldImplementation(configurationFile);
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
                
                return new XmlFileKeyValueStore(fileSystem, configurationFile);
            }

            /// <summary>
            /// a rough copy of the implementation from around https://github.com/OctopusDeploy/OctopusShared/tree/fac3c1dbdb51d3cf71ec5513f2c24d76b18568ee
            /// </summary>
            class OldImplementation 
            {
                private readonly string configurationFile;
                private readonly IDictionary<string, object> settings = new Dictionary<string, object>();

                public OldImplementation(string configurationFile)
                {
                    this.configurationFile = configurationFile;
                }

                private void SetInternal(string name, string value,
                    ProtectionLevel protectionLevel = ProtectionLevel.None)
                {
                    if (name == null) throw new ArgumentNullException(nameof(name));

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        Write(name, null);
                        Save();
                        return;
                    }

                    if (protectionLevel == ProtectionLevel.MachineKey)
                    {
                        value = MachineKeyEncrypter.Current.Encrypt(value);
                    }

                    Write(name, value);
                    Save();
                }

                public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
                {
                    if (name == null) throw new ArgumentNullException(nameof(name));

                    if (typeof(TData) == typeof(string))
                        SetInternal(name, (string) (object) value, protectionLevel);
                    else
                        SetInternal(name, JsonConvert.SerializeObject(value), protectionLevel);
                }

                private void Write(string key, object value)
                {
                    settings[key] = value;
                }

                public void Save()
                {
                    var settings = new XmlSettingsRoot();
                    foreach (var key in this.settings.Keys.OrderBy(k => k))
                    {
                        settings.Settings.Add(new XmlSetting { Key = key, Value = this.settings[key]?.ToString() });
                    }

                    var serializer = new XmlSerializer(typeof (XmlSettingsRoot));
                    using (var stream = new FileStream(configurationFile, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        stream.SetLength(0);
                        using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                        {
                            using (var xmlWriter = new XmlTextWriter(streamWriter))
                            {
                                xmlWriter.Formatting = System.Xml.Formatting.Indented;

                                serializer.Serialize(xmlWriter, settings);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests to make sure we can read & write all the types correctly
        /// </summary>
        [TestFixture]
        class RoundTripTests : RoundTripTestBaseFixture
        {

            protected override XmlFileKeyValueStore SetupDataAndReloadKeyValueStore()
            {
                var configurationFile = System.IO.Path.GetTempFileName();
                var fileSystem = new OctopusPhysicalFileSystem();
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
                
                return new XmlFileKeyValueStore(fileSystem, configurationFile);
            }
        }
    }
}
