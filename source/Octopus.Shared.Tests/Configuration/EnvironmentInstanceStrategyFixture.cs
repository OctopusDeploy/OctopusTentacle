#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;

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
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<string>(new [] { "OCTOPUS_HOME", "Foo" }));

            var results = EnvironmentConfigurationStrategy.LoadFromEnvironment(reader, mapper);
            results.Count.Should().Be(2, because: "a value for all supported variables is returned, even if it is set to null");
            results!["OCTOPUS_HOME"].Should().Be(".", because: "values should be able to contain an equals sign");
            results!["Foo"].Should().BeNull(because: "values should be able to contain an equals sign");
        }

        [Test]
        public void IsNotConfiguredWhenNonDynamicStartupType()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<string>(new[] { "OCTOPUS_HOME " }));

            var subject = new EnvironmentConfigurationStrategy(new StartUpConfigFileInstanceRequest(ApplicationName.OctopusServer, "test.config"), mapper, reader);
            subject.LoadedConfiguration(new ApplicationInstanceRecord()).Should().BeNull(because: "there isn't an instance when the startup request isn't 'dynamic'");
        }

        [Test]
        public void IsNotConfiguredWhenEnvFileInIncomplete()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<string>(new[] { "OCTOPUS_HOME " }));
            mapper.ConfigState.Returns(ConfigState.None);

            var subject = new EnvironmentConfigurationStrategy(new StartUpDynamicInstanceRequest(ApplicationName.OctopusServer), mapper, reader);
            subject.LoadedConfiguration(new ApplicationInstanceRecord()).Should().BeNull(because: "there isn't an instance when there is no config");
        }

        [Test]
        public void IsConfiguredWhenEnvironmentVariablesAreComplete()
        {
            var reader = Substitute.For<IEnvironmentVariableReader>();
            reader.Get("OCTOPUS_HOME").Returns(".");
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<string>(new[] { "OCTOPUS_HOME" }));
            mapper.ConfigState.Returns(ConfigState.Complete);

            var subject = new EnvironmentConfigurationStrategy(new StartUpDynamicInstanceRequest(ApplicationName.OctopusServer), mapper, reader);
            subject.LoadedConfiguration(new ApplicationInstanceRecord()).Should().NotBeNull(because: "there is an instance when there is a complete config");
            mapper.Received(1).SetEnvironmentValues(Arg.Is<Dictionary<string, string?>>(v => v.Count() == 1 && v["OCTOPUS_HOME"] == "."));
        }
    }
}