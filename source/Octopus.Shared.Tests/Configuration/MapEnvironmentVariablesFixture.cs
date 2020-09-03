#nullable enable
using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class MapEnvironmentVariablesFixture
    {
        class TestMapper : MapEnvironmentVariablesToConfigItems
        {
            public TestMapper(string[] supportedConfigurationKeys, string[] requiredEnvironmentVariables, string[] optionalEnvironmentVariables) : base(Substitute.For<ILog>(), supportedConfigurationKeys, requiredEnvironmentVariables, optionalEnvironmentVariables)
            {
            }

            protected override string? MapConfigurationValue(string configurationSettingName)
            {
                if (configurationSettingName == "Octopus.Home")
                    return EnvironmentValues["OCTOPUS_HOME"];
                if (configurationSettingName == "Octopus.Port")
                    return EnvironmentValues["OCTOPUS_PORT"];
                throw new ArgumentException($"Unknown setting {configurationSettingName}");
            }
        }
        
        [Test]
        public void NoSetupConfigState()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            var state = mapper.ConfigState;
            state.Should().Be(ConfigState.None);
        }
        
        [Test]
        public void CompleteSetupConfigState()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" } });
            var state = mapper.ConfigState;
            state.Should().Be(ConfigState.Complete);
        }
        
        [Test]
        public void IncompleteSetupError()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            Action testAction = () => mapper.GetConfigurationValue("Octopus.Home");

            testAction.Should().Throw<InvalidOperationException>()
                .WithMessage("No variable values have been specified.");
        }
                
        [Test]
        public void UnsupportedVariableErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_WRONG", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Unsupported environment variable was provided. 'OCTOPUS_WRONG'");
        }
                
        [Test]
        public void UnsupportedVariablesErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_WRONG", "Test" }, { "OCTOPUS_MORE_WRONG", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Unsupported environment variables were provided. 'OCTOPUS_MORE_WRONG, OCTOPUS_WRONG'");
        }

        [Test]
        public void RequiredVariableErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_OPTIONAL", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Required environment variable was not provided. 'OCTOPUS_HOME'");
        }
        
        [Test]
        public void RequiredVariablesErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_PORT", "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_OPTIONAL", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Required environment variables were not provided. 'OCTOPUS_HOME, OCTOPUS_PORT'");
        }
        
        [Test]
        public void InvalidConfigSettingNameErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" } });
            Action testAction = () => mapper.GetConfigurationValue("Octopus.Port");

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Given configuration setting name is not supported. 'Octopus.Port'");
        }
        
        [Test]
        public void RequiredConfigSettingCanBeRetrieved()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_OPTIONAL" });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" } });
            
            mapper.GetConfigurationValue("Octopus.Home").Should().Be("Test");
        }
        
        [Test]
        public void OptionalConfigSettingCanBeRetrieved()
        {
            var mapper = new TestMapper(new []{ "Octopus.Home", "Octopus.Port" }, new []{ "OCTOPUS_HOME" }, new []{ "OCTOPUS_PORT" });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" }, { "OCTOPUS_PORT", "1234" } });
            
            mapper.GetConfigurationValue("Octopus.Port").Should().Be("1234");
        }
    }
}