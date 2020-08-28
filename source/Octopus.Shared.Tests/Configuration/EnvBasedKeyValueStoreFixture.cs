using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class EnvBasedKeyValueStoreFixture
    {
        [Test]
        public void ThrowsWhenFileCannotBeFound()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var subject = new EnvBasedKeyValueStore(fileSystem);
            Action testAction = () => subject.Get("Octopus.HomeDirectory");

            testAction.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void FindsInLocalDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvBasedKeyValueStoreFixture).Assembly.Location);
            var envPath = Path.Combine(testAssemblyLocation, ".env");
            fileSystem.FileExists(envPath).Returns(true);
            fileSystem.ReadAllText(envPath).Returns(TestFileContent(new []{ "Octopus.HomeDirectory=." }));
            var subject = new EnvBasedKeyValueStore(fileSystem);
            var dir = subject.Get("Octopus.HomeDirectory");
            dir.Should().Be(".");
        }

        [Test]
        public void CommentsGetIgnored()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvBasedKeyValueStoreFixture).Assembly.Location);
            var envPath = Path.Combine(testAssemblyLocation, ".env");
            fileSystem.FileExists(envPath).Returns(true);
            fileSystem.ReadAllText(envPath).Returns(TestFileContent(new []{ "", "# some comment to test", "Octopus.HomeDirectory=." }));
            var subject = new EnvBasedKeyValueStore(fileSystem);
            var dir = subject.Get("Octopus.HomeDirectory");
            dir.Should().Be(".");
        }

        [Test]
        public void FindsInParentDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvBasedKeyValueStoreFixture).Assembly.Location);
            var envPath = Path.Combine(GetParentPath(testAssemblyLocation), ".env");
            fileSystem.FileExists(Arg.Any<string>()).Returns(c => (string)c.Args()[0] == envPath);
            
            fileSystem.ReadAllText(envPath).Returns(TestFileContent(new []{ "Octopus.HomeDirectory=." }));
            
            var subject = new EnvBasedKeyValueStore(fileSystem);
            
            var dir = subject.Get("Octopus.HomeDirectory");
            dir.Should().Be(".");
        }

        [Test]
        public void FindsInRootDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvBasedKeyValueStoreFixture).Assembly.Location);
            var envPath = Path.Combine(Directory.GetDirectoryRoot(testAssemblyLocation), ".env");
            fileSystem.FileExists(Arg.Any<string>()).Returns(c => (string)c.Args()[0] == envPath);
            
            fileSystem.ReadAllText(envPath).Returns(TestFileContent(new []{ "Octopus.HomeDirectory=." }));
            
            var subject = new EnvBasedKeyValueStore(fileSystem);
            
            var dir = subject.Get("Octopus.HomeDirectory");
            dir.Should().Be(".");
        }

        [Test]
        public void ThrowWhenAnEntryIsInvalid()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvBasedKeyValueStoreFixture).Assembly.Location);
            var envPath = Path.Combine(testAssemblyLocation, ".env");
            fileSystem.FileExists(envPath).Returns(true);
            fileSystem.ReadAllText(envPath).Returns(TestFileContent(new []{ "Octopus.HomeDirectory=.", "Broken" }));
            var subject = new EnvBasedKeyValueStore(fileSystem);
            Action testAction = () => subject.Get("Octopus.HomeDirectory");
            testAction.Should().Throw<ArgumentException>();
        }
        
        [Test]
        public void LoadsExpectedResults()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvBasedKeyValueStoreFixture).Assembly.Location);
            var envPath = Path.Combine(testAssemblyLocation, ".env");
            fileSystem.FileExists(envPath).Returns(true);
            fileSystem.ReadAllText(envPath).Returns(TestFileContent(new []{ "Octopus.HomeDirectory=.", "Foo=Bar" }));
            var subject = new EnvBasedKeyValueStore(fileSystem);
            var value = subject.Get("Foo");
            value.Should().Be("Bar");
        }

        string TestFileContent(string[] content)
        {
            var lines = new[] { "" }.Union(content).Union(new[] { "" }).ToArray();
            var textContent = string.Join(Environment.NewLine, lines);
            return textContent;
        }

        string GetParentPath(string path)
        {
            var lastPathSeparator = path.LastIndexOf(Path.DirectorySeparatorChar);
            return path.Substring(0, lastPathSeparator);
        }
    }
}