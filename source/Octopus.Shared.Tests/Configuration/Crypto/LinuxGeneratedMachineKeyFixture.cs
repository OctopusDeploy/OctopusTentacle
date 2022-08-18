using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration.Crypto;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration.Crypto
{
    [TestFixture]
    public class LinuxGeneratedMachineKeyFixture
    {
        IOctopusFileSystem fileSystem;
        static string ValidLengthString = "816490f117f14c7a8fa2697781ed795b";
            
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
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] {ValidLengthString.Substring(1)});
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