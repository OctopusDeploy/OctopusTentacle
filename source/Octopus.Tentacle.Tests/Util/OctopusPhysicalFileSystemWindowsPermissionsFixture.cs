#pragma warning disable CA1416
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Util
{
    [TestFixture]
    [WindowsTest]
    public class OctopusPhysicalFileSystemWindowsPermissionsFixture
    {
        static readonly SecurityIdentifier Users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        static readonly SecurityIdentifier LocalSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        static readonly SecurityIdentifier Administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        string directory;
        string configurationFile;
        OctopusPhysicalFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var directoryInfo = Directory.CreateDirectory(directory);
            var directorySecurity = directoryInfo.GetAccessControl();
            directorySecurity.AddAccessRule(new FileSystemAccessRule(Users,
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            directoryInfo.SetAccessControl(directorySecurity);

            configurationFile = Path.Combine(directory, "Tentacle.config");
            File.WriteAllText(configurationFile, "<octopus-settings></octopus-settings>");
            fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
        }

        [Test]
        public void RestrictingPermissionsToTheOwnerRemovesAccessForOtherUsers()
        {
            IdentitiesWithAccess().Should().Contain(Users);

            fileSystem.RestrictFilePermissionsToOwner(configurationFile).Should().BeTrue();

            new FileInfo(configurationFile).GetAccessControl().AreAccessRulesProtected.Should().BeTrue();
            IdentitiesWithAccess().Should().NotContain(Users);
            IdentitiesWithAccess().Should().Contain(new[] { LocalSystem, Administrators, WindowsIdentity.GetCurrent().User! });
            File.ReadAllText(configurationFile).Should().Be("<octopus-settings></octopus-settings>");
        }

        [Test]
        public void RestrictingPermissionsToTheOwnerTwiceChangesNothingTheSecondTime()
        {
            fileSystem.RestrictFilePermissionsToOwner(configurationFile).Should().BeTrue();

            fileSystem.RestrictFilePermissionsToOwner(configurationFile).Should().BeFalse();
        }

        [Test]
        public void GrantingFullControlLetsTheServiceAccountReadARestrictedFile()
        {
            fileSystem.RestrictFilePermissionsToOwner(configurationFile);
            var usersName = Users.Translate(typeof(NTAccount)).Value;

            fileSystem.TryGrantFullControl(configurationFile, usersName).Should().BeTrue();

            IdentitiesWithAccess().Should().Contain(Users);
        }

        [Test]
        public void GrantingFullControlToAnUnknownAccountFails()
        {
            fileSystem.TryGrantFullControl(configurationFile, @".\no-such-user-" + Guid.NewGuid().ToString("N").Substring(0, 8)).Should().BeFalse();
        }

        SecurityIdentifier[] IdentitiesWithAccess()
            => new FileInfo(configurationFile).GetAccessControl()
                .GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Where(r => r.AccessControlType == AccessControlType.Allow)
                .Select(r => (SecurityIdentifier)r.IdentityReference)
                .ToArray();
    }
}
