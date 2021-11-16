using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Octopus.Tentacle.Hardener
{
    class Program
    {
        public static void Main(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            RemoveInheritanceWhilePreservingExistingEntries(currentDirectory);
            RemoveWriteAccessRulesForBuiltInUsers(currentDirectory);
        }

        static void RemoveInheritanceWhilePreservingExistingEntries(string directory)
        {
            var accessControl = Directory.GetAccessControl(directory, AccessControlSections.Access);
            accessControl.SetAccessRuleProtection(true, true);
            Directory.SetAccessControl(directory, accessControl);
        }

        static void RemoveWriteAccessRulesForBuiltInUsers(string directory)
        {
            var identifier = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var accessControl = Directory.GetAccessControl(directory, AccessControlSections.Access);

            bool IsInheritedWritePermission(FileSystemAccessRule rule)
                => rule.FileSystemRights.HasFlag(FileSystemRights.CreateFiles) || rule.FileSystemRights.HasFlag(FileSystemRights.AppendData);

            var writeRules = accessControl.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Where(r => r.IdentityReference.Value == identifier.Value)
                .Where(r => r.AccessControlType == AccessControlType.Allow)
                .Where(IsInheritedWritePermission)
                .ToList();

            if (!writeRules.Any())
                return;

            foreach (var rule in writeRules)
            {
                accessControl.RemoveAccessRule(rule);
            }

            Directory.SetAccessControl(directory, accessControl);
        }
    }
}