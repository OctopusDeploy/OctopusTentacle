#if NET
using System;
using System.IO;
using System.Runtime.Versioning;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Configuration
{
    [Platform(Exclude = "Win", Reason = "Unix file modes only")]
    [UnsupportedOSPlatform("windows")]
    public class LinuxConfigurationFilePermissionsTests
    {
        [Test]
        public void WhenAWorldReadableConfigIsRestrictedThenOnlyItsOwnerCanReadIt()
        {
            var configurationFile = Path.GetTempFileName();
            File.SetUnixFileMode(configurationFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            new LinuxConfigurationFilePermissions(new InMemoryLog()).RestrictToCurrentUser(configurationFile);

            File.GetUnixFileMode(configurationFile).Should().Be(LinuxConfigurationFilePermissions.OwnerOnly);
        }

        [Test]
        public void WhenAnEmptyConfigIsCreatedThenOnlyItsOwnerCanReadIt()
        {
            var configurationFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            new LinuxConfigurationFilePermissions(new InMemoryLog()).CreateRestrictedToCurrentUser(configurationFile, "<octopus-settings />");

            File.GetUnixFileMode(configurationFile).Should().Be(LinuxConfigurationFilePermissions.OwnerOnly);
        }

        [Test]
        public void WhenTheConfigIsASymlinkThenItIsNotGivenToTheServiceUser()
        {
            var target = Path.GetTempFileName();
            var configurationFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.CreateSymbolicLink(configurationFile, target);
            var log = new InMemoryLog();

            new LinuxConfigurationFilePermissions(log).RestrictToServiceUser(configurationFile, "nobody");

            log.GetLog().Should().Contain("symbolic link");
        }
    }
}
#endif
