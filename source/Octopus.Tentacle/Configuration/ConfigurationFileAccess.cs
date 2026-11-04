using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Octopus.Tentacle.Configuration
{
#pragma warning disable CA1416
    public static class ConfigurationFileAccess
    {
        public static void RestrictToAdministratorsAndCurrentUser(string configurationFile)
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(Allow(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl));
            security.AddAccessRule(Allow(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl));
            security.AddAccessRule(Allow(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl));
            new FileInfo(configurationFile).SetAccessControl(security);
        }

        public static void GrantModify(string configurationFile, string username)
        {
            // sc.exe accepts ".\user" for local accounts, NTAccount does not
            if (username.StartsWith(@".\", StringComparison.Ordinal))
                username = Environment.MachineName + username.Substring(1);

            var file = new FileInfo(configurationFile);
            var security = file.GetAccessControl();
            security.AddAccessRule(Allow(new NTAccount(username), FileSystemRights.Modify));
            file.SetAccessControl(security);
        }

        static FileSystemAccessRule Allow(IdentityReference identity, FileSystemRights rights) => new(identity, rights, AccessControlType.Allow);
    }
#pragma warning restore CA1416
}
