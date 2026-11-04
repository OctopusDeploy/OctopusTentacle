using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceManagerFixture
    {
        [Test]
        [WindowsTest]
        public void CreateInstance_NewConfigurationFile_IsOnlyAccessibleToSystemAdministratorsAndCurrentUser()
        {
            var homeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var configurationFile = Path.Combine(homeDirectory, "Tentacle.config");
            try
            {
                var log = Substitute.For<ISystemLog>();
                var manager = new ApplicationInstanceManager(ApplicationName.Tentacle, new StartUpRegistryInstanceRequest("Test"), new OctopusPhysicalFileSystem(log), log, Substitute.For<IApplicationInstanceStore>(), new Lazy<IWritableHomeConfiguration>(() => Substitute.For<IWritableHomeConfiguration>()));

                manager.CreateInstance("Test", configurationFile, homeDirectory);

#pragma warning disable CA1416
                var expectedSids = new[]
                {
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value,
                    WindowsIdentity.GetCurrent().User!.Value
                }.Distinct();

                var security = new FileInfo(configurationFile).GetAccessControl();
                security.AreAccessRulesProtected.Should().BeTrue();
                security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>()
                    .Select(r => r.IdentityReference.Value)
                    .Distinct()
                    .Should()
                    .BeEquivalentTo(expectedSids);
#pragma warning restore CA1416
            }
            finally
            {
                if (Directory.Exists(homeDirectory))
                    Directory.Delete(homeDirectory, true);
            }
        }
    }
}
