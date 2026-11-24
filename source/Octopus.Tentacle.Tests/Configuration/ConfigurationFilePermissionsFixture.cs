#if NET
using System;
using System.IO;
using System.Runtime.Versioning;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    [UnsupportedOSPlatform("windows")]
    public class ConfigurationFilePermissionsFixture
    {
        [SetUp]
        public void SkipOnWindows()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Unix file modes only");
            }
        }

        [Test]
        public void WhenAWorldReadableConfigIsRestrictedThenOnlyItsOwnerCanReadIt()
        {
            var configurationFile = Path.GetTempFileName();
            File.SetUnixFileMode(configurationFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            ConfigurationFilePermissions.RestrictToOwner(configurationFile, new InMemoryLog());

            File.GetUnixFileMode(configurationFile).Should().Be(ConfigurationFilePermissions.OwnerOnly);
        }

        [Test]
        public void WhenAnEmptyConfigIsCreatedThenOnlyItsOwnerCanReadIt()
        {
            var configurationFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            ConfigurationFilePermissions.CreateEmpty(configurationFile, "<octopus-settings />");

            File.GetUnixFileMode(configurationFile).Should().Be(ConfigurationFilePermissions.OwnerOnly);
        }

        [Test]
        public void WhenTheConfigIsASymlinkThenItIsNotGivenToTheServiceUser()
        {
            var target = Path.GetTempFileName();
            var configurationFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.CreateSymbolicLink(configurationFile, target);
            var log = new InMemoryLog();

            ConfigurationFilePermissions.RestrictToServiceUser(configurationFile, "nobody", log);

            log.GetLog().Should().Contain("symbolic link");
        }
    }
}
#endif
