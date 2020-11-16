using System;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Tests.Configuration
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
    class XmlConsoleKeyValueStoreFixture
    {
        [Test]
        public void WritesSortedXmlUsingCorrectTypes()
        {
            var result = new XDocument();
            var settings = new XmlConsoleKeyValueStore(s =>
            {
                Console.WriteLine(s);
                result = XDocument.Parse(s);
            });
            settings.Set("group1.setting2", 123);
            settings.Set("group1.setting1", true);
            settings.Set<string>("group2.setting3", "a string");
            settings.Set("group3.setting4",
                new MyObject
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
                });
            settings.Set<string>("group4.setting5", null);
            settings.Set<MyObject>("group4.setting6", null);
            settings.Set("group4.setting7", SomeEnum.SomeOtherEnumValue);
            settings.Set<SomeEnum?>("group4.setting8", null);
            settings.Save();

            var expected = XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<octopus-settings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <set key=""group1.setting1"">True</set>
  <set key=""group1.setting2"">123</set>
  <set key=""group2.setting3"">a string</set>
  <set key=""group3.setting4"">{""BooleanField"":true,""IntField"":10,""EnumField"":""SomeOtherEnumValue"",""ArrayField"":[{""Id"":1},{""Id"":2},{""Id"":3}]}</set>
  <set key=""group4.setting5""/>
  <set key=""group4.setting6""/>
  <set key=""group4.setting7"">SomeOtherEnumValue</set>
  <set key=""group4.setting8""/>
</octopus-settings>");
            result.Should().BeEquivalentTo(expected);
        }
    }
}