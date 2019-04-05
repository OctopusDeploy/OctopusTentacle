using System;
using NUnit.Framework;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    class JsonConsoleKeyValueStoreFixture
    {
        [Test]
        public void WritesSortedJsonUsingCorrectTypes()
        {
            string result = null;
            var settings = new JsonConsoleKeyValueStore(s =>
            {
                Console.WriteLine(s);
                result = s;
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

            Assert.That(result.Replace("\r\n", "\n"), Is.EqualTo("{\n  \"group1.setting1\": true,\n  \"group1.setting2\": 123,\n  \"group2.setting3\": \"a string\",\n  \"group3.setting4\": \"{\\\"BooleanField\\\":true,\\\"IntField\\\":10,\\\"ArrayField\\\":[{\\\"Id\\\":1},{\\\"Id\\\":2},{\\\"Id\\\":3}]}\"\n}"));
        }
    }
}
