#nullable enable
using System;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class EnvBasedKeyValueStoreFixture
    {
        class TestMapper : MapEnvironmentVariablesToConfigItems
        {
            public TestMapper(string[] supportedConfigurationKeys, string[] supportedEnvironmentVariables) : base(Substitute.For<ILog>(), supportedConfigurationKeys, new string[0], supportedEnvironmentVariables)
            {
            }

            protected override string? MapConfigurationValue(string configurationSettingName)
            {
                if (configurationSettingName == "Octopus.Home")
                    return EnvironmentValues.ContainsKey("OCTOPUS_HOME") ? EnvironmentValues["OCTOPUS_HOME"] : null;
                if (configurationSettingName == "Foo")
                    return EnvironmentValues.ContainsKey("Foo") ? EnvironmentValues["Foo"] : null;
                return null;
            }
        }
        
        [Test]
        public void CommentsGetIgnored()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "", "# some comment to test", "OCTOPUS_HOME=." }));
            var mapper = new TestMapper(new [] { "Octopus.Home" }, new [] { "OCTOPUS_HOME" });
            
            var subject = new EnvFileBasedKeyValueStore(fileSystem, fileLocator, mapper);
            var dir = subject.Get("Octopus.Home");
            dir.Should().Be(".");
        }

        [Test]
        public void ThrowWhenAnEntryIsInvalid()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "OCTOPUS_HOME=.", "Broken" }));
            var mapper = new TestMapper(new [] { "Octopus.Home" }, new [] { "OCTOPUS_HOME" });

            var subject = new EnvFileBasedKeyValueStore(fileSystem, fileLocator, mapper);
            
            Action testAction = () => subject.Get("Octopus.Home");
            testAction.Should().Throw<ArgumentException>();
        }
        
        [Test]
        public void LoadsExpectedResults()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "OCTOPUS_HOME=.", "Foo=Bar==" }));
            var mapper = new TestMapper(new [] { "Octopus.Home", "Foo" }, new [] { "OCTOPUS_HOME", "Foo" });
            
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