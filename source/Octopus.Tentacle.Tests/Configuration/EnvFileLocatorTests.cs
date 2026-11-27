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
    public class EnvFileLocatorTests
    {
        [Test]
        public void ReturnsNullWhenFileCannotBeFound()
        {
            var subject = CreateSubject(Substitute.For<IOctopusFileSystem>());
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

            var subject = CreateSubject(fileSystem);

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

            var subject = CreateSubject(fileSystem);

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

            var subject = CreateSubject(fileSystem);

            var envFile = subject.LocateEnvFile();
            envFile.Should().Be(envPath, "the .env file is discoverable at the root of the directory tree");
        }

        static EnvFileLocator CreateSubject(IOctopusFileSystem fileSystem)
            => new(fileSystem, Substitute.For<ILogFileOnlyLogger>());

        string GetParentPath(string path)
        {
            var lastPathSeparator = path.LastIndexOf(Path.DirectorySeparatorChar);
            return path.Substring(0, lastPathSeparator);
        }
    }
}