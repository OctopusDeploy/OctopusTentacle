using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Tests.Configuration
{
    class MyNestedObject
    {
        public int Id { get; set; }
    }
        
    class MyObject
    {
        public bool BooleanField { get; set; }
        public int IntField { get; set; }
        public MyNestedObject[] ArrayField { get; set; }
    }
    
    [TestFixture]
    class XmlConsoleKeyValueStoreFixture
    {
        [Test]
        public void WritesSortedXmlUsingCorrectTypes()
        {
            XDocument result = new XDocument();
            var settings = new XmlConsoleKeyValueStore(s =>
            {
                Console.WriteLine(s);
                result = XDocument.Parse(s);
            });
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
            settings.Save();

            var expected = XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<octopus-settings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <set key=""group1.setting1"">True</set>
  <set key=""group1.setting2"">123</set>
  <set key=""group2.setting3"">a string</set>
  <set key=""group3.setting4"">{""BooleanField"":true,""IntField"":10,""ArrayField"":[{""Id"":1},{""Id"":2},{""Id"":3}]}</set>
</octopus-settings>");
            result.Should().BeEquivalentTo(expected);
        }
    }
}
