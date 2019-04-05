using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    class XmlFileKeyValueStoreFixture
    {
        private string configurationFile;
        private IOctopusFileSystem fileSystem;

        [OneTimeSetUp]
        public void Setup()
        {
            configurationFile = System.IO.Path.GetTempFileName();
            fileSystem = new OctopusPhysicalFileSystem();
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            var settings = new XmlFileKeyValueStore(fileSystem, configurationFile);
            settings.Set("group1.setting2", 123);
            settings.Set("group1.setting1", true);
            settings.Set<string>("group2.setting3", "a string");
            settings.Set("group3.setting4", new MyObject
            {
                IntField = 10, BooleanField = true, ArrayField = new[]
                {
                    new MyNestedObject {Id = 1},
                    new MyNestedObject {Id = 2},
                    new MyNestedObject {Id = 3}
                }
            });
            settings.Set<string>("group4.setting5", null);
            settings.Set<MyObject>("group4.setting6", null);
            settings.Save();
        }

        [Test]
        public void WritesSortedXmlUsingCorrectTypes()
        {
            var fileContents = XDocument.Parse(fileSystem.ReadAllText(configurationFile));

            var expected = XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<octopus-settings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <set key=""group1.setting1"">True</set>
  <set key=""group1.setting2"">123</set>
  <set key=""group2.setting3"">a string</set>
  <set key=""group3.setting4"">{""BooleanField"":true,""IntField"":10,""ArrayField"":[{""Id"":1},{""Id"":2},{""Id"":3}]}</set>
  <set key=""group4.setting5""/>
  <set key=""group4.setting6""/>
</octopus-settings>");
            fileContents.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void CanReadXml()
        {
            var settings = new XmlFileKeyValueStore(fileSystem, configurationFile);

            settings.Get("group1.setting1", false).Should().BeTrue();
            settings.Get("group1.setting2", 1).Should().Be(123);
            settings.Get("group2.setting3", "").Should().Be("a string");
            var nestedObject = settings.Get<MyObject>("group3.setting4", null);
            nestedObject.IntField.Should().Be(10);
            nestedObject.BooleanField.Should().BeTrue();
            nestedObject.ArrayField.Length.Should().Be(3);
            nestedObject.ArrayField[0].Id.Should().Be(1);
            nestedObject.ArrayField[1].Id.Should().Be(2);
            nestedObject.ArrayField[2].Id.Should().Be(3);
            settings.Get<string>("group4.setting5", null).Should().Be(null);
            settings.Get<MyObject>("group4.setting6", null).Should().Be(null);
        }
    }
}
