using System;
using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Security.Principal;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    internal class TestUserPrincipal
    {
        public TestUserPrincipal(string username, string password = "Password01!")
        {
            try
            {
                using (var principalContext = new PrincipalContext(ContextType.Machine))
                {
                    UserPrincipal? principal = null;
                    try
                    {
                        principal = UserPrincipal.FindByIdentity(principalContext, IdentityType.Name, username);
                        if (principal != null)
                        {
                            Console.WriteLine($"The Windows User Account named '{username}' already exists, making sure the password is set correctly...");
                            principal.SetPassword(password);
                            principal.Save();
                        }
                        else
                        {
                            Console.WriteLine($"Trying to create a Windows User Account on the local machine called '{username}'...");
                            principal = new UserPrincipal(principalContext);
                            principal.Name = username;
                            principal.SetPassword(password);
                            principal.Save();
                        }

                        // Remember the pertinent details
                        SamAccountName = principal.SamAccountName;
                        Sid = principal.Sid;
                        Password = password;
                    }
                    finally
                    {
                        principal?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to find or create the Windows User Account called '{username}': {ex.Message}");
                throw;
            }
        }

        public SecurityIdentifier Sid { get; }
        public string NTAccountName => Sid.Translate(typeof(NTAccount)).ToString();
        public string DomainName => NTAccountName.Split(new[] { '\\' }, 2)[0];
        public string UserName => NTAccountName.Split(new[] { '\\' }, 2)[1];
        public string SamAccountName { get; }
        public string Password { get; }

        public TestUserPrincipal EnsureIsMemberOfGroup(string groupName)
        {
            Console.WriteLine($"Ensuring the Windows User Account called '{UserName}' is a member of the '{groupName}' group...");
            using (var principalContext = new PrincipalContext(ContextType.Machine))
            using (var principal = UserPrincipal.FindByIdentity(principalContext, IdentityType.Sid, Sid.Value))
            {
                if (principal == null) throw new Exception($"Couldn't find a user account for {UserName} by the SID {Sid.Value}");
                using (var group = GroupPrincipal.FindByIdentity(principalContext, IdentityType.Name, groupName))
                {
                    if (group == null) throw new Exception($"Couldn't find a group with the name {groupName}");
                    if (!group.Members.Contains(principal))
                    {
                        group.Members.Add(principal);
                        group.Save();
                    }
                }
            }

            return this;
        }

        public void Delete()
        {
            using (var principalContext = new PrincipalContext(ContextType.Machine))
            {
                UserPrincipal? principal = null;

                try
                {
                    principal = UserPrincipal.FindByIdentity(principalContext, IdentityType.Name, UserName);
                    if (principal == null)
                    {
                        Console.WriteLine($"The Windows User Account named {UserName} doesn't exist, nothing to do...");
                        return;
                    }

                    Console.WriteLine($"The Windows User Account named {UserName} exists, deleting...");
                    principal.Delete();
                }
                finally
                {
                    principal?.Dispose();
                }
            }
        }

        public NetworkCredential GetCredential()
        {
            return new NetworkCredential(UserName, Password, DomainName);
        }

        public override string ToString()
        {
            return NTAccountName;
        }
    }
}