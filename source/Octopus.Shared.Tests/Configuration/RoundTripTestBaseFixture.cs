using FluentAssertions;
using NUnit.Framework;
using Octopus.Configuration;

namespace Octopus.Shared.Tests.Configuration
{
    internal abstract class RoundTripTestBaseFixture
    {
        protected abstract IKeyValueStore SetupKeyValueStore();

        private IKeyValueStore reloadedSettings;

        [OneTimeSetUp]
        public void Setup()
        {
            reloadedSettings = SetupKeyValueStore();
        }

        [Test]
        public void ReadsBooleanValue()
        {
            reloadedSettings.Get("group1.setting1", false).Should().BeTrue();
        }
            
        [Test]
        public void ReadsIntValue()
        {
            reloadedSettings.Get("group1.setting2", 1).Should().Be(123);
        }
            
        [Test]
        public void ReadsStringValue()
        {
            reloadedSettings.Get("group2.setting3", "").Should().Be("a string");
        }
            
        [Test]
        public void ReadsNestedObjectValue()
        {
            var nestedObject = reloadedSettings.Get<MyObject>("group3.setting4", null);
            nestedObject.IntField.Should().Be(10);
            nestedObject.BooleanField.Should().BeTrue();
            nestedObject.ArrayField.Length.Should().Be(3);
            nestedObject.ArrayField[0].Id.Should().Be(1);
            nestedObject.ArrayField[1].Id.Should().Be(2);
            nestedObject.ArrayField[2].Id.Should().Be(3);
        }
            
        [Test]
        public void ReadsNullStringValue()
        {
            reloadedSettings.Get<string>("group4.setting5", null).Should().Be(null);
        }

        [Test]
        public void ReadsNullObjectValue()
        {
            reloadedSettings.Get<MyObject>("group4.setting6", null).Should().Be(null);
        }
            
        [Test]
        public void ReadsEncryptedBooleanValue()
        {
            reloadedSettings.Get("group5.setting1", false, ProtectionLevel.MachineKey).Should().BeTrue();
        }
            
        [Test]
        public void ReadsEncryptedIntValue()
        {
            reloadedSettings.Get("group5.setting2", 1, ProtectionLevel.MachineKey).Should().Be(123);
        }
            
        [Test]
        public void ReadsEncryptedStringValue()
        {
            reloadedSettings.Get("group5.setting3", "", ProtectionLevel.MachineKey).Should().Be("a string");
        }
            
        [Test]
        public void ReadsEncryptedNestedObjectValue()
        {
            var nestedObject = reloadedSettings.Get<MyObject>("group5.setting4", null, ProtectionLevel.MachineKey);
            nestedObject.IntField.Should().Be(10);
            nestedObject.BooleanField.Should().BeTrue();
            nestedObject.ArrayField.Length.Should().Be(3);
            nestedObject.ArrayField[0].Id.Should().Be(1);
            nestedObject.ArrayField[1].Id.Should().Be(2);
            nestedObject.ArrayField[2].Id.Should().Be(3);
        }
            
        [Test]
        public void ReadsEncryptedNullStringValue()
        {
            reloadedSettings.Get<string>("group5.setting5", null, ProtectionLevel.MachineKey).Should().Be(null);
        }

        [Test]
        public void ReadsEncryptedNullObjectValue()
        {
            reloadedSettings.Get<MyObject>("group5.setting6", null, ProtectionLevel.MachineKey).Should().Be(null);
        }
    }
}