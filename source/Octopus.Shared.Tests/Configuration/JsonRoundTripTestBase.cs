using FluentAssertions;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    /// <summary>
    /// Tests to make sure we can read & write all the types correctly
    /// </summary>
    abstract class JsonRoundTripTestBase : CurrentRoundTripTestBase
    {
        protected IKeyValueStore SetupData()
        {
            FileSystem.OverwriteFile(ConfigurationFile, @"{}");
            var configurationObject = new MyObject
            {
                IntField = 10,
                BooleanField = true,
                EnumField = SomeEnum.SomeOtherEnumValue,
                ArrayField = new[]
                {
                    new MyNestedObject {Id = 1},
                    new MyNestedObject {Id = 2},
                    new MyNestedObject {Id = 3}
                }
            };
            
            var settings = new JsonFileKeyValueStore(ConfigurationFile, FileSystem, autoSaveOnSet: false, isWriteOnly: true);
            settings.Set("group1.setting2", 123);
            settings.Set("group1.setting1", true);
            settings.Set<string>("group2.setting3", "a string");
            settings.Set("group3.setting4", configurationObject);
            settings.Set<string>("group4.setting5", null);
            settings.Set<MyObject>("group4.setting6", null);
            settings.Set("group4.setting7", SomeEnum.SomeOtherEnumValue);
            settings.Set<SomeEnum?>("group4.setting8", null);
            settings.Set("group5.setting2", 123, ProtectionLevel.MachineKey);
            settings.Set("group5.setting1", true, ProtectionLevel.MachineKey);
            settings.Set<string>("group5.setting3", "a string", ProtectionLevel.MachineKey);
            settings.Set("group5.setting4", configurationObject, ProtectionLevel.MachineKey);
            settings.Set<string>("group5.setting5", null, ProtectionLevel.MachineKey);
            settings.Set<MyObject>("group5.setting6", null, ProtectionLevel.MachineKey);
            settings.Set("group5.setting7", SomeEnum.SomeOtherEnumValue, ProtectionLevel.MachineKey);
            settings.Set<SomeEnum?>("group5.setting8", null, ProtectionLevel.MachineKey);
            settings.Save();

            return settings;
        }
    }

    [TestFixture]
    class JsonRoundTripTestsWithoutReReadFromFile : JsonRoundTripTestBase
    {
        protected override IKeyValueStore SetupKeyValueStore()
        {
            return SetupData();
        }
    }

    [TestFixture]
    class JsonRoundTripTestsWithReReadFromFile : JsonRoundTripTestBase
    {
        protected override IKeyValueStore SetupKeyValueStore()
        {
            SetupData();
            return new JsonFileKeyValueStore(ConfigurationFile, FileSystem, autoSaveOnSet: false, isWriteOnly: false);
        }
    }
}