#if NET
using System;
using System.IO;
using System.Runtime.Versioning;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    [UnsupportedOSPlatform("windows")]
    public class ConfigurationFilePermissionsFixture
    {
        [Test]
        public void WhenAWorldReadableConfigIsRestrictedThenOnlyItsOwnerCanReadIt()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Unix file modes only");
                return;
            }

            // Arrange
            var configurationFile = Path.GetTempFileName();
            File.SetUnixFileMode(configurationFile, (UnixFileMode)0b110_100_100);

            // Act
            ConfigurationFilePermissions.RestrictToOwner(configurationFile);

            // Assert
            File.GetUnixFileMode(configurationFile).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        [Test]
        public void WhenTheConfigIsASymlinkThenItIsNotGivenToTheServiceUser()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Unix only");
                return;
            }

            // Arrange
            var target = Path.GetTempFileName();
            var configurationFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.CreateSymbolicLink(configurationFile, target);

            // Act
            var error = ConfigurationFilePermissions.TryRestrictToServiceUser(configurationFile, Environment.UserName);

            // Assert
            error.Should().Contain("symbolic link");
        }
    }
}
#endif
