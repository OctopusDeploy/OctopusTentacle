using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class EnvFileLocatorFixture
    {
        IOctopusFileSystem fileSystem;
        ILogFileOnlyLogger log;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            log = Substitute.For<ILogFileOnlyLogger>();
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
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvFileConfigurationContributorFixture).Assembly.Location);
            var envPath = Path.Combine(testAssemblyLocation, ".env");
            fileSystem.FileExists(envPath).Returns(true);

            var subject = new EnvFileLocator(fileSystem, log);

            var envFile = subject.LocateEnvFile();
            envFile.Should().Be(envPath, "the .env file is discoverable from the working directory");
        }

        [Test]
        public void FindsInParentDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvFileConfigurationContributorFixture).Assembly.Location);
            var envPath = Path.Combine(GetParentPath(testAssemblyLocation), ".env");
            fileSystem.FileExists(Arg.Any<string>()).Returns(c => (string)c.Args()[0] == envPath);

            var subject = new EnvFileLocator(fileSystem, log);

            var envFile = subject.LocateEnvFile();
            envFile.Should().Be(envPath, "the .env file is discoverable up the directory tree");
        }

        [Test]
        public void FindsInRootDirectory()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            var testAssemblyLocation = Path.GetDirectoryName(typeof(EnvFileConfigurationContributorFixture).Assembly.Location);
            var envPath = Path.Combine(Directory.GetDirectoryRoot(testAssemblyLocation), ".env");
            fileSystem.FileExists(Arg.Any<string>()).Returns(c => (string)c.Args()[0] == envPath);

            var subject = new EnvFileLocator(fileSystem, log);

            var envFile = subject.LocateEnvFile();
            envFile.Should().Be(envPath, "the .env file is discoverable at the root of the directory tree");
        }

        string GetParentPath(string path)
        {
            var lastPathSeparator = path.LastIndexOf(Path.DirectorySeparatorChar);
            return path.Substring(0, lastPathSeparator);
        }
    }
}