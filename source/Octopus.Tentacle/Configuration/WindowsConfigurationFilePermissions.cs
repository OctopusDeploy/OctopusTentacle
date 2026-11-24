using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Octopus.Tentacle.Core.Diagnostics;
#if NET
using System.Runtime.Versioning;
#endif

namespace Octopus.Tentacle.Configuration
{
    // A new file inherits its folder's ACL. Under the default home, C:\Octopus, that comes from C:\, which lets every
    // authenticated user modify the file. The config's protected settings are encrypted with DPAPI's LocalMachine scope,
    // so anyone who can read the file can decrypt them. Only SYSTEM, Administrators and the account Tentacle runs as
    // should be able to access it.
#if NET
    [SupportedOSPlatform("windows")]
#endif
    static class WindowsConfigurationFilePermissions
    {
        static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
        static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);

        public static void CreateEmpty(string configurationPath, string contents)
        {
            var security = new FileSecurity();
            Restrict(security);

            // The ACL is applied as the file is created, so it never has the folder's ACL.
#if NET
            using var stream = new FileInfo(configurationPath).Create(FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security);
#else
            using var stream = new FileStream(configurationPath, FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security);
#endif
            using var writer = new StreamWriter(stream);
            writer.Write(contents);
        }

        // Runs as the account Tentacle runs as, so it can't lock that account out. An ACL that no longer inherits from
        // its folder has already been restricted, by Tentacle or by an administrator, so it is left alone.
        public static void RestrictToOwner(string configurationPath, ISystemLog log)
        {
            try
            {
                var file = new FileInfo(configurationPath);
                var security = file.GetAccessControl();
                if (security.AreAccessRulesProtected)
                {
                    return;
                }

                Restrict(security);
                file.SetAccessControl(security);
                log.Info($"Restricted `{configurationPath}` to SYSTEM, Administrators and {WindowsIdentity.GetCurrent().Name}");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PrivilegeNotHeldException)
            {
                log.Warn($"Unable to restrict `{configurationPath}`, so other users may be able to read it: {ex.Message}. Run `service --reconfigure --username <service account>` as an administrator to restrict it.");
            }
        }

        // Grants the service account access before the service is started with it, so it can read the restricted file.
        public static void RestrictToServiceUser(string configurationPath, string userName, ISystemLog log)
        {
            try
            {
                var serviceAccount = ResolveAccount(userName);
                var file = new FileInfo(configurationPath);
                var security = file.GetAccessControl();
                Restrict(security);
                if (serviceAccount != LocalSystem)
                {
                    security.AddAccessRule(new FileSystemAccessRule(serviceAccount, FileSystemRights.Modify, AccessControlType.Allow));
                }

                file.SetAccessControl(security);
                log.Info($"Restricted `{configurationPath}` to SYSTEM, Administrators, {WindowsIdentity.GetCurrent().Name} and `{userName}`");
            }
            catch (Exception ex)
            {
                log.Warn($"Unable to restrict `{configurationPath}` to `{userName}`: {ex.Message.TrimEnd('.')}. Only SYSTEM, Administrators and the service account should be able to read and write the configuration file, so check its permissions.");
            }
        }

        // Stops inheriting from the folder, which drops entries such as Authenticated Users, and keeps any entries set on
        // the file itself, such as a service account granted access by an earlier install.
        static void Restrict(FileSecurity security)
        {
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, AccessControlType.Allow));

            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                security.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.FullControl, AccessControlType.Allow));
            }
        }

        static SecurityIdentifier ResolveAccount(string userName)
        {
            // sc.exe accepts these, NTAccount doesn't.
            if (string.Equals(userName, "LocalSystem", StringComparison.OrdinalIgnoreCase))
            {
                return LocalSystem;
            }

            if (userName.StartsWith(@".\", StringComparison.Ordinal))
            {
                userName = Environment.MachineName + userName.Substring(1);
            }

            return (SecurityIdentifier)new NTAccount(userName).Translate(typeof(SecurityIdentifier));
        }
    }
}
