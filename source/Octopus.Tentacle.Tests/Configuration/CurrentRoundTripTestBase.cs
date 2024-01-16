using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Configuration
{
    /// <summary>
    /// Adds tests for property types that were not supported by the old implementation (c.f. <see cref="XmlFileKeyValueStoreFixture.BackwardsCompatFixture" />)
    /// </summary>
    abstract class CurrentRoundTripTestBase : RoundTripTestBaseFixture
    {
        [Test]
        public void ReadsEnumValue()
        {
            ReloadedSettings.Get<SomeEnum>("group4.setting7").Should().Be(SomeEnum.SomeOtherEnumValue);
        }

        [Test]
        public void ReadsNullableEnumValue()
        {
            ReloadedSettings.Get<SomeEnum?>("group4.setting8").Should().BeNull();
        }

        [Test]
        public void ReadsEncryptedEnumValue()
        {
            ReloadedSettings.Get("group5.setting7", SomeEnum.SomeEnumValue, ProtectionLevel.MachineKey).Should().Be(SomeEnum.SomeOtherEnumValue);
        }

        [Test]
        public void ReadsEncryptedNullableEnumValue()
        {
            ReloadedSettings.Get<SomeEnum?>("group5.setting8", null, ProtectionLevel.MachineKey).Should().BeNull();
        }
    }
}