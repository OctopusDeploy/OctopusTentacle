#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class EnvironmentInstanceStrategyFixture
    {
        [Test]
        public void LoadsExpectedResultsForSupportedVariables()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            reader.Get("OCTOPUS_HOME").Returns(".");
            reader.Get("Foo").Returns((string?)null);
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>(new[] { EnvironmentVariable.PlaintText("OCTOPUS_HOME"), EnvironmentVariable.PlaintText("Foo") }));

            var results = EnvironmentConfigurationContributor.LoadFromEnvironment(Substitute.For<ILogFileOnlyLogger>(), reader, mapper);
            results.Count.Should().Be(2, "a value for all supported variables is returned, even if it is set to null");
            results!["OCTOPUS_HOME"].Should().Be(".", "values should be able to contain an equals sign");
            results!["Foo"].Should().BeNull("values should be able to contain an equals sign");
        }

        [Test]
        public void IsConfiguredWhenNonDynamicStartupType()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>(new[] { EnvironmentVariable.PlaintText("OCTOPUS_HOME") }));

            var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
            subject.LoadContributedConfiguration().Should().NotBeNull("there is an instance if there are any environment variables");
        }

        [Test]
        public void IsNotConfiguredWhenEnvFileIsEmpty()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>());

            var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
            subject.LoadContributedConfiguration().Should().BeNull("there isn't an instance when there is no config");
        }

        [Test]
        public void IsConfiguredWhenEnvironmentVariablesAreComplete()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            reader.Get("OCTOPUS_HOME").Returns(".");
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>(new[] { EnvironmentVariable.PlaintText("OCTOPUS_HOME") }));

            var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
            subject.LoadContributedConfiguration().Should().NotBeNull("there is an instance when there is a complete config");
            mapper.Received(1).SetEnvironmentValues(Arg.Is<Dictionary<string, string?>>(v => v.Count() == 1 && v["OCTOPUS_HOME"] == "."));
        }
    }
}