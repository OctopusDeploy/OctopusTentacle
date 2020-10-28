#nullable enable
using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class MapEnvironmentVariablesFixture
    {
        class TestMapper : MapsEnvironmentVariablesToConfigItems
        {
            public TestMapper(string[] supportedConfigurationKeys, EnvironmentVariable[] requiredEnvironmentVariables, EnvironmentVariable[] optionalEnvironmentVariables) : base(supportedConfigurationKeys, requiredEnvironmentVariables, optionalEnvironmentVariables)
            {
            }

            protected override string? MapConfigurationValue(string configurationSettingName)
            {
                if (configurationSettingName == "Octopus.Port")
                    return EnvironmentValues["OCTOPUS_PORT"];
                if (configurationSettingName == "Octopus.ListenPrefixes")
                    return EnvironmentValues["OCTOPUS_LISTEN_PREFIXES"];
                throw new ArgumentException($"Unknown setting {configurationSettingName}");
            }
        }

        [Test]
        public void IncompleteSetupError()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL") });
            Action testAction = () => mapper.GetConfigurationValue("Octopus.Port");

            testAction.Should().Throw<InvalidOperationException>()
                .WithMessage("No variable values have been specified.");
        }

        [Test]
        public void UnsupportedVariableErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL") });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_WRONG", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Unsupported environment variable was provided. 'OCTOPUS_WRONG'");
        }

        [Test]
        public void UnsupportedVariablesErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL") });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_WRONG", "Test" }, { "OCTOPUS_MORE_WRONG", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Unsupported environment variables were provided. 'OCTOPUS_MORE_WRONG, OCTOPUS_WRONG'");
        }

        [Test]
        public void RequiredVariableErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL") });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_OPTIONAL", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Required environment variable was not provided. 'OCTOPUS_PORT'");
        }

        [Test]
        public void RequiredVariablesErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT"), EnvironmentVariable.PlaintText("OCTOPUS_FORCE_SSL") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL") });
            Action testAction = () => mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_OPTIONAL", "Test" } });

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Required environment variables were not provided. 'OCTOPUS_FORCE_SSL, OCTOPUS_PORT'");
        }

        [Test]
        public void InvalidConfigSettingNameErrorIsDescriptive()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL") });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_PORT", "1234" } });
            Action testAction = () => mapper.GetConfigurationValue("Octopus.ForceSSL");

            testAction.Should().Throw<ArgumentException>()
                .WithMessage("Given configuration setting name is not supported. 'Octopus.ForceSSL'");
        }

        [Test]
        public void RequiredConfigSettingCanBeRetrieved()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL") });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_PORT", "1234" } });

            mapper.GetConfigurationValue("Octopus.Port").Should().Be("1234");
        }

        [Test]
        public void OptionalConfigSettingCanBeRetrieved()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port", "Octopus.ListenPrefixes" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL"), EnvironmentVariable.PlaintText("OCTOPUS_LISTEN_PREFIXES") });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" }, { "OCTOPUS_PORT", "1234" } });

            mapper.GetConfigurationValue("Octopus.ListenPrefixes").Should().BeNull();
        }

        [Test]
        public void SharedConfigSettingCanBeRetrieved()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port", "Octopus.ListenPrefixes" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL"), EnvironmentVariable.PlaintText("OCTOPUS_LISTEN_PREFIXES") });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" }, { "OCTOPUS_PORT", "1234" } });

            mapper.GetConfigurationValue("Octopus.Home").Should().Be("Test", because: "shared settings get contributed by the base mapper");
        }

        [Test]
        public void SetValuesAreImmutable()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port", "Octopus.ListenPrefixes" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL"), EnvironmentVariable.PlaintText("OCTOPUS_LISTEN_PREFIXES") });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" }, { "OCTOPUS_PORT", "1234" } });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test2" }, { "OCTOPUS_PORT", "12345" } });

            mapper.GetConfigurationValue("Octopus.Home").Should().Be("Test", because: "settings can only be set once, highest priority strategy will contribute first");
        }

        [Test]
        public void NullValuesAreWritten()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port", "Octopus.ListenPrefixes" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL"), EnvironmentVariable.PlaintText("OCTOPUS_LISTEN_PREFIXES") });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", null }, { "OCTOPUS_PORT", "1234" } });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" }, { "OCTOPUS_PORT", "12345" } });

            mapper.GetConfigurationValue("Octopus.Home").Should().Be("Test", because: "blank settings can still be set");
            mapper.GetConfigurationValue("Octopus.Port").Should().Be("1234", because: "settings can only be set once, highest priority strategy will contribute first");
        }

        [Test]
        public void BlankValuesAreWritten()
        {
            var mapper = new TestMapper(new []{ "Octopus.Port", "Octopus.ListenPrefixes" }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_PORT") }, new []{ EnvironmentVariable.PlaintText("OCTOPUS_OPTIONAL"), EnvironmentVariable.PlaintText("OCTOPUS_LISTEN_PREFIXES") });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "" }, { "OCTOPUS_PORT", "1234" } });
            mapper.SetEnvironmentValues(new Dictionary<string, string?> { { "OCTOPUS_HOME", "Test" }, { "OCTOPUS_PORT", "12345" } });

            mapper.GetConfigurationValue("Octopus.Home").Should().Be("Test", because: "blank settings can still be set");
            mapper.GetConfigurationValue("Octopus.Port").Should().Be("1234", because: "settings can only be set once, highest priority strategy will contribute first");
        }
    }
}