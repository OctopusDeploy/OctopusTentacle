using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class EnvFileLocatorFixture
    {
        IOctopusFileSystem fileSystem;
        ILog log;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            log = Substitute.For<ILog>();
        }
        
        [Test]
        public void ReturnsNullWhenFileCannotBeFound()
        {
            var subject = new EnvFileLocator(fileSystem, log);
            var envFile = subject.LocateEnvFile();

            envFile.Should().BeNull();
        }

        [Test]
        public void FindsInLocalDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvFileInstanceStrategyFixture).Assembly.Location);
            var envPath = Path.Combine(testAssemblyLocation, ".env");
            fileSystem.FileExists(envPath).Returns(true);

            var subject = new EnvFileLocator(fileSystem, log);

            var envFile = subject.LocateEnvFile();
            envFile.Should().Be(envPath, because: "the .env file is discoverable from the working directory");
        }

        
        [Test]
        public void FindsInParentDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvFileInstanceStrategyFixture).Assembly.Location);
            var envPath = Path.Combine(GetParentPath(testAssemblyLocation), ".env");
            fileSystem.FileExists(Arg.Any<string>()).Returns(c => (string)c.Args()[0] == envPath);
            
            var subject = new EnvFileLocator(fileSystem, log);

            var envFile = subject.LocateEnvFile();
            envFile.Should().Be(envPath, because: "the .env file is discoverable up the directory tree");
        }

        [Test]
        public void FindsInRootDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvFileInstanceStrategyFixture).Assembly.Location);
            var envPath = Path.Combine(Directory.GetDirectoryRoot(testAssemblyLocation), ".env");
            fileSystem.FileExists(Arg.Any<string>()).Returns(c => (string)c.Args()[0] == envPath);
            
            var subject = new EnvFileLocator(fileSystem, log);

            var envFile = subject.LocateEnvFile();
            envFile.Should().Be(envPath, because: "the .env file is discoverable at the root of the directory tree");
        }

        string GetParentPath(string path)
        {
            var lastPathSeparator = path.LastIndexOf(Path.DirectorySeparatorChar);
            return path.Substring(0, lastPathSeparator);
        }
    }
}