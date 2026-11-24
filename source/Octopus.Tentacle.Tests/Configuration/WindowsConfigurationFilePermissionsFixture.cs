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
    [TestFixture]
    [WindowsTest]
#if NET
    [SupportedOSPlatform("windows")]
#endif
    public class WindowsConfigurationFilePermissionsFixture
    {
        static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
        static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
        static readonly SecurityIdentifier AuthenticatedUsers = new(WellKnownSidType.AuthenticatedUserSid, null);
        static readonly SecurityIdentifier NetworkService = new(WellKnownSidType.NetworkServiceSid, null);
        static readonly SecurityIdentifier LocalService = new(WellKnownSidType.LocalServiceSid, null);

        string directory = null!;

        [SetUp]
        public void SetUp()
        {
            // Mimics the default home, C:\Octopus, which inherits Authenticated Users: Modify from C:\
            directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var directoryInfo = Directory.CreateDirectory(directory);
            var directorySecurity = directoryInfo.GetAccessControl();
            directorySecurity.AddAccessRule(new FileSystemAccessRule(AuthenticatedUsers,
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            directoryInfo.SetAccessControl(directorySecurity);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
        }

        [Test]
        public void WhenAnEmptyConfigIsCreatedThenOnlySystemAdministratorsAndTheCurrentUserCanAccessIt()
        {
            var configurationFile = Path.Combine(directory, "Tentacle.config");

            ConfigurationFilePermissions.CreateEmpty(configurationFile, "<octopus-settings />");

            File.ReadAllText(configurationFile).Should().Be("<octopus-settings />");
            var security = new FileInfo(configurationFile).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            Identities(security).Should().BeEquivalentTo(RestrictedIdentities());
        }

        [Test]
        public void WhenAConfigInheritsItsFolderPermissionsThenTheyAreReplacedButItsOwnEntriesAreKept()
        {
            var configurationFile = CreateInheritingConfig();
            GrantModify(configurationFile, NetworkService);

            ConfigurationFilePermissions.RestrictToOwner(configurationFile, new InMemoryLog());

            var security = new FileInfo(configurationFile).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            Identities(security).Should().BeEquivalentTo(RestrictedIdentities().Append(NetworkService.Value));
        }

        [Test]
        public void WhenAConfigNoLongerInheritsItsFolderPermissionsThenItIsLeftAlone()
        {
            var configurationFile = CreateInheritingConfig();
            var file = new FileInfo(configurationFile);
            var security = file.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
            file.SetAccessControl(security);

            ConfigurationFilePermissions.RestrictToOwner(configurationFile, new InMemoryLog());

            Identities(file.GetAccessControl()).Should().Contain(AuthenticatedUsers.Value);
        }

        [Test]
        public void WhenRestrictedToTheServiceUserThenOnlyThatUserSystemAndAdministratorsCanAccessIt()
        {
            var configurationFile = CreateInheritingConfig();
            var log = new InMemoryLog();

            ConfigurationFilePermissions.RestrictToServiceUser(configurationFile, AccountName(LocalService), log);

            log.GetLog().Should().NotContain("Unable to restrict");
            var security = new FileInfo(configurationFile).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            Identities(security).Should().BeEquivalentTo(RestrictedIdentities().Append(LocalService.Value));
            Rights(security, LocalService).Should().HaveFlag(FileSystemRights.Modify).And.NotHaveFlag(FileSystemRights.ChangePermissions);
        }

        [Test]
        public void WhenTheServiceUserIsLocalSystemThenNoOtherUserIsGrantedAccess()
        {
            var configurationFile = CreateInheritingConfig();

            ConfigurationFilePermissions.RestrictToServiceUser(configurationFile, "LocalSystem", new InMemoryLog());

            Identities(new FileInfo(configurationFile).GetAccessControl()).Should().BeEquivalentTo(RestrictedIdentities());
        }

        [Test]
        public void WhenTheServiceUserCannotBeFoundThenThePermissionsAreLeftAlone()
        {
            var configurationFile = CreateInheritingConfig();
            var log = new InMemoryLog();

            ConfigurationFilePermissions.RestrictToServiceUser(configurationFile, @".\tentacle-no-such-user", log);

            log.GetLog().Should().Contain("Unable to restrict");
            var security = new FileInfo(configurationFile).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeFalse();
            Identities(security).Should().Contain(AuthenticatedUsers.Value);
        }

        string CreateInheritingConfig()
        {
            var configurationFile = Path.Combine(directory, "Tentacle.config");
            File.WriteAllText(configurationFile, "<octopus-settings />");
            Identities(new FileInfo(configurationFile).GetAccessControl()).Should().Contain(AuthenticatedUsers.Value, "the config should start out inheriting its folder's permissions");
            return configurationFile;
        }

        static void GrantModify(string path, SecurityIdentifier identity)
        {
            var file = new FileInfo(path);
            var security = file.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.Modify, AccessControlType.Allow));
            file.SetAccessControl(security);
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
