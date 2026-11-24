using System;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Tests.Configuration;

namespace Octopus.Tentacle.Tests.Commands
{
    public class ShowConfigurationOutputTests
    {
        [Test]
        public void WhenSettingsHaveDottedNames_AndMixedTypes_ThenTheyAreWrittenAsSortedNestedJson()
        {
            // Arrange
            var output = new ShowConfigurationOutput();
            output.Add("group1.setting2", 123);
            output.Add("group1.setting1", true);
            output.Add("group2.setting3", "a string");
            output.Add("group3.setting4",
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
            output.Add("group4.setting5", null);
            output.Add("group4.setting6", "  ");
            output.Add("group4.setting7", SomeEnum.SomeEnumValue);

            // Act
            var json = output.ToJson();

            // Assert
            Assert.That(json.Replace("\r\n", "\n"), Is.EqualTo("{\n  \"group1\": {\n    \"setting1\": true,\n    \"setting2\": 123\n  },\n  \"group2\": {\n    \"setting3\": \"a string\"\n  },\n  \"group3\": {\n    \"setting4\": {\n      \"BooleanField\": true,\n      \"IntField\": 10,\n      \"EnumField\": \"SomeEnumValue\",\n      \"ArrayField\": [\n        {\n          \"Id\": 1\n        },\n        {\n          \"Id\": 2\n        },\n        {\n          \"Id\": 3\n        }\n      ]\n    }\n  },\n  \"group4\": {\n    \"setting5\": null,\n    \"setting6\": null,\n    \"setting7\": \"SomeEnumValue\"\n  }\n}"));
        }
    }
}
