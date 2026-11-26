using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
#if NET
using System.Runtime.Versioning;
#endif

namespace Octopus.Tentacle.Tests.Integration.Configuration
{
    [IntegrationTestTimeout]
    [WindowsTest]
#if NET
    [SupportedOSPlatform("windows")]
#endif
    public class WindowsConfigurationFilePermissionsTests : IntegrationTest
    {
        [Test]
        public async Task WhenAnInstanceIsCreatedThenOnlySystemAdministratorsAndTheCurrentUserCanAccessItsConfig()
        {
            using var instance = await new TentacleInstanceBuilder()
                .WithName($"ConfigPermissions-{Guid.NewGuid():N}")
                .WithHomeDirectory(HomeDirectoryAnyAuthenticatedUserCanModify())
                .Build(Logger, CancellationToken);

            var security = new FileInfo(instance.ConfigurationFile).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Select(r => r.IdentityReference.Value)
                .Distinct()
                .Should()
                .BeEquivalentTo(new[]
                    {
                        new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value,
                        WindowsIdentity.GetCurrent().User!.Value
                    }
                    .Distinct());
        }

        // Like the default home, C:\Octopus, which inherits Authenticated Users: Modify from C:\. A config that
        // inherited its folder's permissions would let every user read it.
        static TemporaryDirectory HomeDirectoryAnyAuthenticatedUserCanModify()
        {
            var home = new TemporaryDirectory();
            var directory = new DirectoryInfo(home.DirectoryPath);
            var security = directory.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            directory.SetAccessControl(security);
            return home;
        }
    }
}
