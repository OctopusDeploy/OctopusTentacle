#if NET
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Configuration
{
    [LinuxTest]
    [NonParallelizable]
    [UnsupportedOSPlatform("windows")]
    public class LinuxConfigurationFilePermissionsTests
    {
        [Test]
        [RequiresSudoOnLinux]
        public async Task WhenRestrictedToTheServiceUserThenOnlyThatUserOwnsAndCanReadTheConfig()
        {
            using var user = new LinuxTestUserPrincipal("octo-config-perm-test");
            var configurationFile = Path.GetTempFileName();
            File.SetUnixFileMode(configurationFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            var log = new InMemoryLog();

            new LinuxConfigurationFilePermissions(log).RestrictToServiceUser(configurationFile, user.UserName);

            log.GetLog().Should().NotContain("Unable to restrict");
            File.GetUnixFileMode(configurationFile).Should().Be(LinuxConfigurationFilePermissions.OwnerOnly);
            var owner = await new CommandLineInvocation("/bin/bash", $"-c \"stat -c '%U' {configurationFile}\"").ExecuteCommandAsync();
            owner.Infos.Single().Should().Be(user.UserName);
        }
    }
}
#endif
