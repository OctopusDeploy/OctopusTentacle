using System;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
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
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "", "# some comment to test", "Octopus.HomeDirectory=." }));
            
            var subject = new EnvBasedKeyValueStore(fileSystem, fileLocator);
            var dir = subject.Get("Octopus.HomeDirectory");
            dir.Should().Be(".");
        }

        [Test]
        public void ThrowWhenAnEntryIsInvalid()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "Octopus.HomeDirectory=.", "Broken" }));
            var subject = new EnvBasedKeyValueStore(fileSystem, fileLocator);
            Action testAction = () => subject.Get("Octopus.HomeDirectory");
            testAction.Should().Throw<ArgumentException>();
        }
        
        [Test]
        public void LoadsExpectedResults()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var fileLocator = Substitute.For<IEnvFileLocator>();
            fileLocator.LocateEnvFile().Returns("test");
            fileSystem.ReadAllText("test").Returns(TestFileContent(new []{ "Octopus.HomeDirectory=.", "Foo=Bar==" }));
            var subject = new EnvBasedKeyValueStore(fileSystem, fileLocator);
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