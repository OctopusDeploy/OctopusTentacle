#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Tests.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class EnvFileConfigurationContributorFixture
    {
        IOctopusFileSystem fileSystem = null!;
        IEnvFileLocator fileLocator = null!;
        IMapEnvironmentValuesToConfigItems mapper = null!;
        
        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            fileLocator = Substitute.For<IEnvFileLocator>();
            mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
        }

        [Test]
        public void CommentsGetIgnored()
        {
            fileLocator.LocateEnvFile().Returns("test");
            SetTextContents( "", "# some comment to test", "OCTOPUS_HOME=.");
            SetSupportedEnvironmentVariables("OCTOPUS_HOME");

            var results = EnvFileConfigurationContributor.LoadFromEnvFile(fileLocator, fileSystem, mapper);
            results.Should().NotBeNull("the envFile exists");
            results!.Count.Should().Be(1, "blank lines and comments should be ignored");
        }

        [Test]
        public void ThrowWhenAnEntryIsInvalid()
        {
            fileLocator.LocateEnvFile().Returns("test");
            SetTextContents("OCTOPUS_HOME=.", "Broken");
            SetSupportedEnvironmentVariables("OCTOPUS_HOME");

            Action testAction = () => EnvFileConfigurationContributor.LoadFromEnvFile(fileLocator, fileSystem, mapper);
            testAction.Should().Throw<ArgumentException>().WithMessage("Line 2 is not formatted correctly");
        }

        [Test]
        public void LoadsExpectedResults()
        {
            fileLocator.LocateEnvFile().Returns("test");
            SetTextContents("OCTOPUS_HOME=.", "Foo=Bar==");
            SetSupportedEnvironmentVariables("OCTOPUS_HOME", "Foo");

            var results = EnvFileConfigurationContributor.LoadFromEnvFile(fileLocator, fileSystem, mapper);
            results.Should().NotBeNull("the envFile exists");
            var value = results!["Foo"];
            value.Should().Be("Bar==", "values should be able to contain an equals sign");
        }

        [Test]
        public void IsNotConfiguredWhenEmptyFile()
        {
            fileLocator.LocateEnvFile().Returns("test");
            SetTextContents(string.Empty);

            var subject = new EnvFileConfigurationContributor(fileSystem, fileLocator, mapper);
            subject.LoadContributedConfiguration().Should().BeNull("there isn't an instance when the file contains no values");
        }

        [Test]
        public void IsNotConfiguredWhenNoEnvFile()
        {
            fileLocator.LocateEnvFile().Returns((string?)null);
         
            var subject = new EnvFileConfigurationContributor(fileSystem, fileLocator, mapper);
            subject.LoadContributedConfiguration().Should().BeNull("there isn't an instance when there is no envFile");
        }

        [Test]
        public void IsIgnoredIfSpecialFlagNotPresent()
        {
            fileLocator.LocateEnvFile().Returns("test");
            SetTextContents("OCTOPUS_HOME=.");
            SetSupportedEnvironmentVariables("OCTOPUS_HOME");

            var subject = new EnvFileConfigurationContributor(fileSystem, fileLocator, mapper);
            subject.LoadContributedConfiguration().Should().BeNull();
        }

        [Test]
        public void IsConfiguredWhenEnvFileExists()
        {
            using (new TemporaryEnvironmentVariable(ApplicationConfigurationContributionFlag.ContributeSettingsFlag, "true"))
            {
                fileLocator.LocateEnvFile().Returns("test");
                SetTextContents("OCTOPUS_HOME=.");
                SetSupportedEnvironmentVariables("OCTOPUS_HOME");
          

                var subject = new EnvFileConfigurationContributor(fileSystem, fileLocator, mapper);
                subject.LoadContributedConfiguration().Should().NotBeNull("there is an instance when there is a file");
            }
        }

        void SetSupportedEnvironmentVariables(params string[] environmentVariableName)
        {
            var hashSet = new HashSet<EnvironmentVariable>(environmentVariableName.Select(c =>  EnvironmentVariable.PlaintText(c)));
            mapper.SupportedEnvironmentVariables.Returns(hashSet);
        }

        void SetTextContents(params string[] content)
        {
            fileSystem.ReadAllText("test").Returns(TestFileContent(content));
        }
        
        string TestFileContent(string[] content)
        {
            var lines = content.ToArray();
            var textContent = string.Join(Environment.NewLine, lines);
            return textContent;
        }
    }
}