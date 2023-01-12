#nullable enable
using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class AggregatedKeyValueStoreFixture
    {
        [Test]
        public void DefaultStringIsHandledCorrectly()
        {
            var mapper1 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper1.GetConfigurationValue("Test").Returns((string?)null);
            var memStore1 = new InMemoryKeyValueStore(mapper1);
            var mapper2 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper2.GetConfigurationValue("Test").Returns("a value");
            var memStore2 = new InMemoryKeyValueStore(mapper2);

            var aggregatedStore = new AggregatedKeyValueStore(new IAggregatableKeyValueStore[] { memStore1, memStore2 });
            var result = aggregatedStore.Get("Test", string.Empty);
            result.Should().Be("a value", "A null from an aggregated store should be treated as null, not the default");
        }

        [Test]
        public void NullableStringIsHandledCorrectly()
        {
            var mapper1 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper1.GetConfigurationValue("Test").Returns((string?)null);
            var memStore1 = new InMemoryKeyValueStore(mapper1);

            var aggregatedStore = new AggregatedKeyValueStore(new IAggregatableKeyValueStore[] { memStore1 });
            var result = aggregatedStore.Get("Test", "a value");
            result.Should().Be("a value", "A null from an aggregated store should be treated as null, not the default");
        }

        [Test]
        public void DefaultIntIsHandledCorrectly()
        {
            var mapper1 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper1.GetConfigurationValue("Test").Returns((string?)null);
            var memStore1 = new InMemoryKeyValueStore(mapper1);

            var aggregatedStore = new AggregatedKeyValueStore(new IAggregatableKeyValueStore[] { memStore1 });
            var result = aggregatedStore.Get("Test", 80);
            result.Should().Be(80, "default should be applied correctly for ints");
        }

        [Test]
        public void NullIntIsHandledCorrectly()
        {
            var mapper1 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper1.GetConfigurationValue("Test").Returns((string?)null);
            var memStore1 = new InMemoryKeyValueStore(mapper1);
            var mapper2 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper2.GetConfigurationValue("Test").Returns("50");
            var memStore2 = new InMemoryKeyValueStore(mapper2);

            var aggregatedStore = new AggregatedKeyValueStore(new IAggregatableKeyValueStore[] { memStore1, memStore2 });
            var result = aggregatedStore.Get("Test", 80);
            result.Should().Be(50, "A null from an aggregated store should be treated as null, not the default");
        }
    }
}