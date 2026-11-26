using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Octopus.Tentacle.Util;
#if NET
using System.Runtime.Versioning;
#endif

namespace Octopus.Tentacle.Tests.Support
{
    // Builds a Tentacle config in a folder that, like the default home C:\Octopus, lets every authenticated user modify
    // the files in it. By default the config inherits the folder's permissions.
#if NET
    [SupportedOSPlatform("windows")]
#endif
    class WindowsConfigurationFileBuilder
    {
        public static readonly SecurityIdentifier AuthenticatedUsers = new(WellKnownSidType.AuthenticatedUserSid, null);

        bool inheritFolderPermissions = true;
        readonly List<SecurityIdentifier> grantedModify = new();

        public WindowsConfigurationFileBuilder WithModifyGrantedTo(SecurityIdentifier identity)
        {
            grantedModify.Add(identity);
            return this;
        }

        // As if Tentacle or an administrator had already restricted it, keeping the folder's entries as its own.
        public WindowsConfigurationFileBuilder NoLongerInheritingFolderPermissions()
        {
            inheritFolderPermissions = false;
            return this;
        }

        public TemporaryWindowsConfigurationFile Build()
        {
            var directory = new TemporaryDirectory(new OctopusPhysicalFileSystem(new InMemoryLog()));
            var directoryInfo = new DirectoryInfo(directory.DirectoryPath);
            var directorySecurity = directoryInfo.GetAccessControl();
            directorySecurity.AddAccessRule(new FileSystemAccessRule(AuthenticatedUsers,
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            directoryInfo.SetAccessControl(directorySecurity);

            var configurationFile = new TemporaryWindowsConfigurationFile(directory, Path.Combine(directory.DirectoryPath, "Tentacle.config"));
            File.WriteAllText(configurationFile.Path, "<octopus-settings />");
            var file = new FileInfo(configurationFile.Path);
            var security = file.GetAccessControl();
            foreach (var identity in grantedModify)
            {
                security.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.Modify, AccessControlType.Allow));
            }

            if (!inheritFolderPermissions)
            {
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
            }

            file.SetAccessControl(security);
            return configurationFile;
        }
    }

    class TemporaryWindowsConfigurationFile : IDisposable
    {
        readonly TemporaryDirectory directory;

        public TemporaryWindowsConfigurationFile(TemporaryDirectory directory, string path)
        {
            this.directory = directory;
            Path = path;
        }

        public string Path { get; }

        public void Dispose() => directory.Dispose();
    }
}
