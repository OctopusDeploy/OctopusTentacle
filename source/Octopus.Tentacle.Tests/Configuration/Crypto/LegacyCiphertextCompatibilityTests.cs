#if !NETFRAMEWORK
using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    // Ciphertext captured from the pre-versioned encryptor (commit 7b1504e), which used a fixed IV.
    // Do not regenerate it with the current code: that would only prove the current code agrees with itself.
    [LinuxOrMacTest]
    public class LegacyCiphertextCompatibilityTests
    {
        [Test]
        public void WhenAValueWasWrittenWithTheMachineIdKeyThenItStillDecrypts()
        {
            // Arrange
            const string legacyCiphertext = "nMmwC8Uzkg/bEwaDChRL86dTMzY4gCyveDi0qCjOWNENjQzd3wYS8lJwMeGXNdEy";
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.FileExists(LinuxMachineIdKey.FileName).Returns(true);
            fileSystem.ReadAllLines(LinuxMachineIdKey.FileName).Returns(new[] { "816490f117f14c7a8fa2697781ed795b" });
            var encryptor = MachineKeyEncryptor.CreateLinuxEncryptor(Substitute.For<ISystemLog>(), fileSystem, ConfigurationEncryptionKey.KeyFilePathFor("Tentacle"), "/etc/octopus/machinekey");

            // Act
            var decrypted = encryptor.Decrypt(legacyCiphertext);

            // Assert
            decrypted.Should().Be("pässwörd 🐙 with spaces & symbols!");
        }

        [Test]
        public void WhenAValueWasWrittenWithoutAMachineIdThenItStillDecrypts()
        {
            // Arrange
            const string legacyCiphertext = "cVzJO4WpcDeZTWcG91jyln6rFJvjf08RkpiAb+FUMcOnzT8uB5emsPmEKS8vyg+p";
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.FileExists("/etc/octopus/machinekey").Returns(true);
            fileSystem.ReadAllText("/etc/octopus/machinekey").Returns("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==");
            var encryptor = MachineKeyEncryptor.CreateLinuxEncryptor(Substitute.For<ISystemLog>(), fileSystem, ConfigurationEncryptionKey.KeyFilePathFor("Tentacle"), "/etc/octopus/machinekey");

            // Act
            var decrypted = encryptor.Decrypt(legacyCiphertext);

            // Assert
            decrypted.Should().Be("pässwörd 🐙 with spaces & symbols!");
        }
    }
}
#endif
