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
            settings.Save();

            Assert.That(result.Replace("\r\n", "\n"), Is.EqualTo("{\n  \"group1.setting1\": true,\n  \"group1.setting2\": 123,\n  \"group2.setting3\": \"a string\"\n}"));
        }
    }
}
