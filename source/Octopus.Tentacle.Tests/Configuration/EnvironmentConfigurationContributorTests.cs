#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    public class EnvironmentConfigurationContributorTests
    {
        [Test]
        public void LoadsExpectedResultsForSupportedVariables()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            var mapper = MapperSupporting("OCTOPUS_HOME", "Foo");
            reader.Get("OCTOPUS_HOME").Returns(".");
            reader.Get("Foo").Returns((string?)null);

            var results = EnvironmentConfigurationContributor.LoadFromEnvironment(Substitute.For<ILogFileOnlyLogger>(), reader, mapper);
            results.Count.Should().Be(2, "a value for all supported variables is returned, even if it is set to null");
            results!["OCTOPUS_HOME"].Should().Be(".", "values should be able to contain an equals sign");
            results!["Foo"].Should().BeNull("values should be able to contain an equals sign");
        }
        
        [Test]
        public void IsNotConfiguredWhenEnvFileIsEmpty()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            var mapper = MapperSupporting();
            var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
            subject.LoadContributedConfiguration().Should().BeNull("there isn't an instance when there is no config");
        }

        [Test]
        public void IsIgnoredIfSpecialFlagNotPresent()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            var mapper = MapperSupporting("OCTOPUS_HOME");
            reader.Get("OCTOPUS_HOME").Returns(".");

            var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
            subject.LoadContributedConfiguration().Should().BeNull();
        }
        
        [Test]
        public void IsConfiguredWhenEnvironmentVariablesAreCompleteAndFlagIsPresent()
        {
            using (new TemporaryEnvironmentVariable(ApplicationConfigurationContributionFlag.ContributeSettingsFlag, "true"))
            {
                var reader = Substitute.For<IEnvironmentVariableReader>();
                var mapper = MapperSupporting("OCTOPUS_HOME");
                reader.Get("OCTOPUS_HOME").Returns(".");

                var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
                subject.LoadContributedConfiguration().Should().NotBeNull("there is an instance when there is a complete config");
                mapper.Received(1).SetEnvironmentValues(Arg.Is<Dictionary<string, string?>>(v => v.Count() == 1 && v["OCTOPUS_HOME"] == "."));
            }
        }
        
        static IMapEnvironmentValuesToConfigItems MapperSupporting(params string[] environmentVariableNames)
        {
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>(environmentVariableNames.Select(EnvironmentVariable.PlaintText)));
            return mapper;
        }
    }
}
