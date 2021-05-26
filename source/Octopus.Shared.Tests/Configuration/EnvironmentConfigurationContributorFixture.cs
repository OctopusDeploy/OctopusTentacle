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
using Octopus.Shared.Tests.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class EnvironmentConfigurationContributorFixture
    {
        IEnvironmentVariableReader reader = null!;
        IMapEnvironmentValuesToConfigItems mapper = null!;
        [SetUp]
        public void Setup()
        {
            
            reader = Substitute.For<IEnvironmentVariableReader>();
            mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
        }
        
        [Test]
        public void LoadsExpectedResultsForSupportedVariables()
        {
            reader.Get("OCTOPUS_HOME").Returns(".");
            reader.Get("Foo").Returns((string?)null);
            SetSupportedEnvironmentVariables("OCTOPUS_HOME", "Foo");
            
            var results = EnvironmentConfigurationContributor.LoadFromEnvironment(Substitute.For<ILogFileOnlyLogger>(), reader, mapper);
            results.Count.Should().Be(2, "a value for all supported variables is returned, even if it is set to null");
            results!["OCTOPUS_HOME"].Should().Be(".", "values should be able to contain an equals sign");
            results!["Foo"].Should().BeNull("values should be able to contain an equals sign");
        }
        
        [Test]
        public void IsNotConfiguredWhenEnvFileIsEmpty()
        {
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>());
            var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
            subject.LoadContributedConfiguration().Should().BeNull("there isn't an instance when there is no config");
        }

        [Test]
        public void IsIgnoredIfSpecialFlagNotPresent()
        {
            reader.Get("OCTOPUS_HOME").Returns(".");
            SetSupportedEnvironmentVariables("OCTOPUS_HOME");

            var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
            subject.LoadContributedConfiguration().Should().BeNull();
        }
        
        [Test]
        public void IsConfiguredWhenEnvironmentVariablesAreCompleteAndFlagIsPresent()
        {
            using (new TemporaryEnvironmentVariable(ApplicationConfigurationContributionFlag.ContributeSettingsFlag, "true"))
            {
                reader.Get("OCTOPUS_HOME").Returns(".");
                SetSupportedEnvironmentVariables("OCTOPUS_HOME");

                var subject = new EnvironmentConfigurationContributor(Substitute.For<ILogFileOnlyLogger>(), mapper, reader);
                subject.LoadContributedConfiguration().Should().NotBeNull("there is an instance when there is a complete config");
                mapper.Received(1).SetEnvironmentValues(Arg.Is<Dictionary<string, string?>>(v => v.Count() == 1 && v["OCTOPUS_HOME"] == "."));
            }
        }
        
        void SetSupportedEnvironmentVariables(params string[] environmentVariableName)
        {
            var hashSet = new HashSet<EnvironmentVariable>(environmentVariableName.Select(c =>  EnvironmentVariable.PlaintText(c)));
            mapper.SupportedEnvironmentVariables.Returns(hashSet);
        }
    }
}