using System;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class EnvBasedKeyValueStoreFixture
    {
        [Test]
        public void CommentsGetIgnored()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "", "# some comment to test", "OCTOPUS_HOME=." }));
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.GetConfigurationValue("Octopus.HomeDirectory").Returns(".");
            
            var subject = new EnvFileBasedKeyValueStore(fileSystem, fileLocator, mapper);
            var dir = subject.Get("Octopus.HomeDirectory");
            dir.Should().Be(".");
        }

        [Test]
        public void ThrowWhenAnEntryIsInvalid()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "OCTOPUS_HOME=.", "Broken" }));
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();

            var subject = new EnvFileBasedKeyValueStore(fileSystem, fileLocator, mapper);
            
            Action testAction = () => subject.Get("Octopus.HomeDirectory");
            testAction.Should().Throw<ArgumentException>();
        }
        
        [Test]
        public void LoadsExpectedResults()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "OCTOPUS_HOME=.", "Foo=Bar==" }));
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            
            var subject = new EnvFileBasedKeyValueStore(fileSystem, fileLocator, mapper);
            
            var value = subject.Get("Foo");
            value.Should().Be("Bar==");
        }

        string TestFileContent(string[] content)
        {
            var lines = new[] { "" }.Union(content).Union(new[] { "" }).ToArray();
            var textContent = string.Join(Environment.NewLine, lines);
            return textContent;
        }
    }
}