using System;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    class JsonHierarchicalConsoleKeyValueStoreFixture
    {
        [Test]
        public void WritesSortedJsonUsingCorrectTypes()
        {
            string result = null;
            var settings = new JsonHierarchicalConsoleKeyValueStore(s =>
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

            Assert.That(result.Replace("\r\n", "\n"), Is.EqualTo("{\n  \"group1\": {\n    \"setting1\": true,\n    \"setting2\": 123\n  },\n  \"group2\": {\n    \"setting3\": \"a string\"\n  },\n  \"group3\": {\n    \"setting4\": {\n      \"BooleanField\": true,\n      \"IntField\": 10,\n      \"EnumField\": \"SomeEnumValue\",\n      \"ArrayField\": [\n        {\n          \"Id\": 1\n        },\n        {\n          \"Id\": 2\n        },\n        {\n          \"Id\": 3\n        }\n      ]\n    }\n  },\n  \"group4\": {\n    \"setting5\": null,\n    \"setting6\": null,\n    \"setting7\": \"SomeEnumValue\",\n    \"setting8\": null\n  }\n}"));
        }
    }
}