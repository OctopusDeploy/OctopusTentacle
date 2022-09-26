using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    [TestFixture]
    public class LinuxGeneratedMachineKeyFixture
    {
        private static readonly string ValidLengthString = "816490f117f14c7a8fa2697781ed795b";
        private IOctopusFileSystem fileSystem;

        [SetUp]
        public void Setup()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
        }

        [Test]
        public void GivenMachineIdFileIsMissing_ThenThrows()
        {
            var keySource = new LinuxMachineIdKey(fileSystem);
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(false);

            Assert.Throws<InvalidOperationException>(() => keySource.Load());
        }

        [Test]
        public void GivenMachineIdIsShort_ThenThrows()
        {
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(true);
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] { ValidLengthString.Substring(1) });
            var keySource = new LinuxMachineIdKey(fileSystem);

            Assert.Throws<InvalidOperationException>(() => keySource.Load());
        }

        [Test]
        public void GivenMachineIdFileIsValidLength_ThenReturnsKeys()
        {
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(true);
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] { ValidLengthString });
            var keySource = new LinuxMachineIdKey(fileSystem);

            var (key, iv) = keySource.Load();
            key.Should().NotBeEmpty();
            iv.Should().NotBeEmpty();
        }
    }
}