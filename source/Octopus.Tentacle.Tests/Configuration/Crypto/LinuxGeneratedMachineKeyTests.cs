using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    public class LinuxGeneratedMachineKeyTests
    {
        const string ValidLengthString = "816490f117f14c7a8fa2697781ed795b";

        [Test]
        public void GivenMachineIdFileIsMissing_ThenThrows()
        {
            var fileSystem = FileSystemWithoutMachineId();
            var keySource = new LinuxMachineIdKey(fileSystem);

            Assert.Throws<InvalidOperationException>(() => keySource.Load());
        }

        [Test]
        public void GivenMachineIdIsShort_ThenThrows()
        {
            var fileSystem = FileSystemWithMachineId(ValidLengthString.Substring(1));
            var keySource = new LinuxMachineIdKey(fileSystem);

            Assert.Throws<InvalidOperationException>(() => keySource.Load());
        }

        [Test]
        public void GivenMachineIdFileIsValidLength_ThenReturnsKeys()
        {
            var fileSystem = FileSystemWithMachineId(ValidLengthString);
            var keySource = new LinuxMachineIdKey(fileSystem);

            var (key, iv) = keySource.Load();
            key.Should().NotBeEmpty();
            iv.Should().NotBeEmpty();
        }

        [Test]
        public void KeyFileIsKeptPerInstanceOutsideTheConfigurationDirectory()
        {
            ConfigurationEncryptionKey.KeyFilePathFor("Tentacle").Should().Be("/var/lib/octopus/tentacle/Tentacle/keys/config-encryption.key");
        }

        [TestCase("")]
        [TestCase("..")]
        [TestCase("../etc")]
        [TestCase("a\\b")]
        public void InstanceNamesThatAreNotASingleDirectoryNameAreRejected(string instanceName)
        {
            Assert.Throws<ArgumentException>(() => ConfigurationEncryptionKey.KeyFilePathFor(instanceName));
        }

        static IOctopusFileSystem FileSystemWithoutMachineId()
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(false);
            return fileSystem;
        }

        static IOctopusFileSystem FileSystemWithMachineId(string machineId)
        {
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(true);
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] { machineId });
            return fileSystem;
        }
    }
}
