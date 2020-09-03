#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
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
            var mapper = Substitute.For<IMapEnvironmentVariablesToConfigItems>();
            mapper.SupportedEnvironmentVariables.Returns(new HashSet<string>(new [] { "OCTOPUS_HOME" }));
            
            var subject = new EnvFileBasedKeyValueStore(fileSystem, fileLocator, mapper);
            subject.Get("Octopus.Home");
            mapper.Received(1).SetEnvironmentValues(Arg.Is<Dictionary<string,string?>>(c => c.Count == 1 && c.ContainsKey("OCTOPUS_HOME")));
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
            testAction.Should().Throw<ArgumentException>().WithMessage("Line 2 is not formatted correctly");
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
            var lines = content.ToArray();
            var textContent = string.Join(Environment.NewLine, lines);
            return textContent;
        }
    }
}