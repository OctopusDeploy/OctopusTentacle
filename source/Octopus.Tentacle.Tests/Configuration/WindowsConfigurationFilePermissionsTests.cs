using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Tests.Support.TestAttributes;
#if NET
using System.Runtime.Versioning;
#endif

namespace Octopus.Tentacle.Tests.Configuration
{
    [WindowsTest]
#if NET
    [SupportedOSPlatform("windows")]
#endif
    public class WindowsConfigurationFilePermissionsTests
    {
        static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
        static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
        static readonly SecurityIdentifier NetworkService = new(WellKnownSidType.NetworkServiceSid, null);
        static readonly SecurityIdentifier LocalService = new(WellKnownSidType.LocalServiceSid, null);
        static readonly SecurityIdentifier AuthenticatedUsers = WindowsConfigurationFileBuilder.AuthenticatedUsers;

        [Test]
        public void WhenAConfigInheritsItsFolderPermissionsThenTheyAreReplacedButItsOwnEntriesAreKept()
        {
            using var configurationFile = new WindowsConfigurationFileBuilder().WithModifyGrantedTo(NetworkService).Build();

            new WindowsConfigurationFilePermissions(new InMemoryLog()).RestrictToCurrentUser(configurationFile.Path);

            var security = new FileInfo(configurationFile.Path).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            Identities(security).Should().BeEquivalentTo(RestrictedIdentities().Append(NetworkService.Value));
        }

        [Test]
        public void WhenAConfigNoLongerInheritsItsFolderPermissionsThenItIsLeftAlone()
        {
            using var configurationFile = new WindowsConfigurationFileBuilder().NoLongerInheritingFolderPermissions().Build();

            new WindowsConfigurationFilePermissions(new InMemoryLog()).RestrictToCurrentUser(configurationFile.Path);

            Identities(new FileInfo(configurationFile.Path).GetAccessControl()).Should().Contain(AuthenticatedUsers.Value);
        }

        [Test]
        public void WhenRestrictedToTheServiceUserThenOnlyThatUserSystemAndAdministratorsCanAccessIt()
        {
            using var configurationFile = new WindowsConfigurationFileBuilder().Build();
            var log = new InMemoryLog();

            new WindowsConfigurationFilePermissions(log).RestrictToServiceUser(configurationFile.Path, AccountName(LocalService));

            log.GetLog().Should().NotContain("Unable to restrict");
            var security = new FileInfo(configurationFile.Path).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            Identities(security).Should().BeEquivalentTo(RestrictedIdentities().Append(LocalService.Value));
            Rights(security, LocalService).Should().HaveFlag(FileSystemRights.Modify).And.NotHaveFlag(FileSystemRights.ChangePermissions);
        }

        [Test]
        public void WhenTheServiceUserIsLocalSystemThenNoOtherUserIsGrantedAccess()
        {
            using var configurationFile = new WindowsConfigurationFileBuilder().Build();

            new WindowsConfigurationFilePermissions(new InMemoryLog()).RestrictToServiceUser(configurationFile.Path, "LocalSystem");

            Identities(new FileInfo(configurationFile.Path).GetAccessControl()).Should().BeEquivalentTo(RestrictedIdentities());
        }

        [Test]
        public void WhenTheServiceUserCannotBeFoundThenThePermissionsAreLeftAlone()
        {
            using var configurationFile = new WindowsConfigurationFileBuilder().Build();
            var log = new InMemoryLog();

            new WindowsConfigurationFilePermissions(log).RestrictToServiceUser(configurationFile.Path, @".\tentacle-no-such-user");

            log.GetLog().Should().Contain("Unable to restrict");
            var security = new FileInfo(configurationFile.Path).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeFalse();
            Identities(security).Should().Contain(AuthenticatedUsers.Value);
        }

        static string[] RestrictedIdentities()
            => new[] { LocalSystem.Value, Administrators.Value, WindowsIdentity.GetCurrent().User!.Value }.Distinct().ToArray();

        static string[] Identities(FileSecurity security)
            => security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Select(r => r.IdentityReference.Value)
                .Distinct()
                .ToArray();

        static FileSystemRights Rights(FileSecurity security, SecurityIdentifier identity)
            => security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Where(r => r.IdentityReference.Value == identity.Value)
                .Aggregate((FileSystemRights)0, (rights, r) => rights | r.FileSystemRights);

        static string AccountName(SecurityIdentifier identity)
            => identity.Translate(typeof(NTAccount)).Value;
    }
}
