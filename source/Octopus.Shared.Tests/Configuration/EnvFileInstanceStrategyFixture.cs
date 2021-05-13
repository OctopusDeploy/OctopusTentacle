#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class EnvFileInstanceStrategyFixture
    {
        [Test]
        public void CommentsGetIgnored()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new[] { "", "# some comment to test", "OCTOPUS_HOME=." }));
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>(new[] { EnvironmentVariable.PlaintText("OCTOPUS_HOME") }));

            var results = EnvFileConfigurationContributor.LoadFromEnvFile(fileLocator, fileSystem, mapper);
            results.Should().NotBeNull("the envFile exists");
            results!.Count.Should().Be(1, "blank lines and comments should be ignored");
        }

        [Test]
        public void ThrowWhenAnEntryIsInvalid()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new[] { "OCTOPUS_HOME=.", "Broken" }));
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>(new[] { EnvironmentVariable.PlaintText("OCTOPUS_HOME") }));

            Action testAction = () => EnvFileConfigurationContributor.LoadFromEnvFile(fileLocator, fileSystem, mapper);
            testAction.Should().Throw<ArgumentException>().WithMessage("Line 2 is not formatted correctly");
        }

        [Test]
        public void LoadsExpectedResults()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new[] { "OCTOPUS_HOME=.", "Foo=Bar==" }));
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<EnvironmentVariable>(new[] { EnvironmentVariable.PlaintText("OCTOPUS_HOME"), EnvironmentVariable.PlaintText("Foo") }));

            var results = EnvFileConfigurationContributor.LoadFromEnvFile(fileLocator, fileSystem, mapper);
            results.Should().NotBeNull("the envFile exists");
            var value = results!["Foo"];
            value.Should().Be("Bar==", "values should be able to contain an equals sign");
        }

        string TestFileContent(string[] content)
        {
            var lines = content.ToArray();
            var textContent = string.Join(Environment.NewLine, lines);
            return textContent;
        }

        [Test]
        public void IsNotConfiguredWhenEmptyFile()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new string[0]));
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();

            var subject = new EnvFileConfigurationContributor(fileSystem, fileLocator, mapper);
            subject.LoadContributedConfiguration().Should().BeNull("there isn't an instance when the file contains no values");
        }

        [Test]
        public void IsNotConfiguredWhenNoEnvFile()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns((string?)null);
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();

            var subject = new EnvFileConfigurationContributor(fileSystem, fileLocator, mapper);
            subject.LoadContributedConfiguration().Should().BeNull("there isn't an instance when there is no envFile");
        }

        [Test]
        public void IsConfiguredWhenEnvFileExists()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new[] { "OCTOPUS_HOME=." }));
            var mapper = Substitute.For<IMapEnvironmentValuesToConfigItems>();
            var hashSet = new HashSet<EnvironmentVariable>(new[] { EnvironmentVariable.PlaintText("OCTOPUS_HOME") });
            mapper.SupportedEnvironmentVariables.Returns(hashSet);

            var subject = new EnvFileConfigurationContributor(fileSystem, fileLocator, mapper);
            subject.LoadContributedConfiguration().Should().NotBeNull("there is an instance when there is a file");
        }
    }
}