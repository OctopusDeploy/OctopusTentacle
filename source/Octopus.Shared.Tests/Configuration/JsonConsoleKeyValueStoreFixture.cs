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
            settings.Set("group3.setting4",
                new MyObject
                {
                    IntField = 10,
                    BooleanField = true,
                    EnumField = SomeEnum.SomeEnumValue,
                    ArrayField = new[]
                    {
                        new MyNestedObject { Id = 1 },
                        new MyNestedObject { Id = 2 },
                        new MyNestedObject { Id = 3 }
                    }
                });
            settings.Set<string>("group4.setting5", null);
            settings.Set<MyObject>("group4.setting6", null);
            settings.Set("group4.setting7", SomeEnum.SomeEnumValue);
            settings.Set<SomeEnum?>("group4.setting8", null);
            settings.Save();

            Assert.That(result.Replace("\r\n", "\n"), Is.EqualTo("{\n  \"group1.setting1\": true,\n  \"group1.setting2\": 123,\n  \"group2.setting3\": \"a string\",\n  \"group3.setting4\": \"{\\\"BooleanField\\\":true,\\\"IntField\\\":10,\\\"EnumField\\\":\\\"SomeEnumValue\\\",\\\"ArrayField\\\":[{\\\"Id\\\":1},{\\\"Id\\\":2},{\\\"Id\\\":3}]}\",\n  \"group4.setting5\": null,\n  \"group4.setting6\": null,\n  \"group4.setting7\": \"SomeEnumValue\",\n  \"group4.setting8\": null\n}"));
        }
    }
}