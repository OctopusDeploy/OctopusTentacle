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
            settings.Save();

            var expected = XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<octopus-settings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <set key=""group1.setting1"">True</set>
  <set key=""group1.setting2"">123</set>
  <set key=""group2.setting3"">a string</set>
</octopus-settings>");
            result.Should().BeEquivalentTo(expected);
        }
    }
}
