#nullable  enable
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class AggregatedKeyValueStoreFixture
    {
        [Test]
        public void DefaultIsHandledCorrectly()
        {
            var mapper1 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper1.GetConfigurationValue("Test").Returns((string?)null);
            var memStore1 = new InMemoryKeyValueStore(mapper1);
            var mapper2 = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper2.GetConfigurationValue("Test").Returns("a value");
            var memStore2 = new InMemoryKeyValueStore(mapper2);

            var aggregatedStore = new AggregatedKeyValueStore(new IKeyValueStore[] { memStore1, memStore2 });
            var result = aggregatedStore.Get("Test", string.Empty);
            result.Should().Be("a value", because: "A null from an aggregated store should be treated as null, not the default");
        }
    }
}