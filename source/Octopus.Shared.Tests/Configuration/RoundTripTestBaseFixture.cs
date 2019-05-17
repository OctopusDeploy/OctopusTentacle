using FluentAssertions;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Shared.Security.Masking;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    internal abstract class RoundTripTestBaseFixture
    {
        protected abstract IKeyValueStore SetupKeyValueStore();
        protected IKeyValueStore ReloadedSettings;
        protected readonly string ConfigurationFile;
        protected readonly OctopusPhysicalFileSystem FileSystem;

        protected RoundTripTestBaseFixture()
        {
            ConfigurationFile = System.IO.Path.GetTempFileName();
            FileSystem = new OctopusPhysicalFileSystem();
        }

        [OneTimeSetUp]
        public void Setup()
        {
            ReloadedSettings = SetupKeyValueStore();
        }

        [Test]
        public void ReadsBooleanValue()
        {
            ReloadedSettings.Get("group1.setting1", false).Should().BeTrue();
        }
            
        [Test]
        public void ReadsIntValue()
        {
            ReloadedSettings.Get("group1.setting2", 1).Should().Be(123);
        }
            
        [Test]
        public void ReadsStringValue()
        {
            ReloadedSettings.Get("group2.setting3", "").Should().Be("a string");
        }
            
        [Test]
        public void ReadsNestedObjectValue()
        {
            var nestedObject = ReloadedSettings.Get<MyObject>("group3.setting4", null);
            nestedObject.IntField.Should().Be(10);
            nestedObject.BooleanField.Should().BeTrue();
            nestedObject.EnumField.Should().Be(SomeEnum.SomeOtherEnumValue);
            nestedObject.ArrayField.Length.Should().Be(3);
            nestedObject.ArrayField[0].Id.Should().Be(1);
            nestedObject.ArrayField[1].Id.Should().Be(2);
            nestedObject.ArrayField[2].Id.Should().Be(3);
        }
            
        [Test]
        public void ReadsNullStringValue()
        {
            ReloadedSettings.Get<string>("group4.setting5", null).Should().Be(null);
        }

        [Test]
        public void ReadsNullObjectValue()
        {
            ReloadedSettings.Get<MyObject>("group4.setting6", null).Should().Be(null);
        }
            
        [Test]
        public void ReadsEncryptedBooleanValue()
        {
            ReloadedSettings.Get("group5.setting1", false, ProtectionLevel.MachineKey).Should().BeTrue();
        }
            
        [Test]
        public void ReadsEncryptedIntValue()
        {
            ReloadedSettings.Get("group5.setting2", 1, ProtectionLevel.MachineKey).Should().Be(123);
        }
            
        [Test]
        public void ReadsEncryptedStringValue()
        {
            ReloadedSettings.Get("group5.setting3", "", ProtectionLevel.MachineKey).Should().Be("a string");
        }
            
        [Test]
        public void ReadsEncryptedNestedObjectValue()
        {
            var nestedObject = ReloadedSettings.Get<MyObject>("group5.setting4", null, ProtectionLevel.MachineKey);
            nestedObject.IntField.Should().Be(10);
            nestedObject.BooleanField.Should().BeTrue();
            nestedObject.EnumField.Should().Be(SomeEnum.SomeOtherEnumValue);
            nestedObject.ArrayField.Length.Should().Be(3);
            nestedObject.ArrayField[0].Id.Should().Be(1);
            nestedObject.ArrayField[1].Id.Should().Be(2);
            nestedObject.ArrayField[2].Id.Should().Be(3);
        }
            
        [Test]
        public void ReadsEncryptedNullStringValue()
        {
            ReloadedSettings.Get<string>("group5.setting5", null, ProtectionLevel.MachineKey).Should().Be(null);
        }

        [Test]
        public void ReadsEncryptedNullObjectValue()
        {
            ReloadedSettings.Get<MyObject>("group5.setting6", null, ProtectionLevel.MachineKey).Should().Be(null);
        }
    }
}