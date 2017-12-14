using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Security.Principal;

namespace Octopus.Shared.Tests.Util
{
    internal class TransientUserPrincipal : IDisposable
    {
        readonly PrincipalContext principalContext;
        readonly UserPrincipal principal;
        readonly string password;

        public TransientUserPrincipal(string name = null, string password = "Password01!", ContextType contextType = ContextType.Machine)
        {
            // We have seen cases where the random username is invalid - trying again should help reduce false-negatives
            // System.DirectoryServices.AccountManagement.PrincipalOperationException : The specified username is invalid.
            var attempts = 0;
            while (true)
            {
                try
                {
                    attempts++;
                    principalContext = new PrincipalContext(contextType);
                    {
                        principal = new UserPrincipal(principalContext);
                        principal.Name = name ?? new string(Guid.NewGuid().ToString("N").ToLowerInvariant().Where(char.IsLetter).ToArray());
                        principal.SetPassword(password);
                        principal.Save();
                        this.password = password;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create the Windows User Account called '{principal.Name}': {ex.Message}");
                    if (attempts >= 5) throw;
                }
            }
        }

        public string NTAccountName => principal.Sid.Translate(typeof(NTAccount)).ToString();
        public string DomainName => NTAccountName.Split(new[] {'\\'}, 2)[0];
        public string UserName => NTAccountName.Split(new[] {'\\'}, 2)[1];
        public string SamAccountName => principal.SamAccountName;
        public string Password => password;
        public NetworkCredential GetCredential() => new NetworkCredential(UserName, Password, DomainName);

        public void Dispose()
        {
            principal.Delete();
            principal.Dispose();
            principalContext.Dispose();
        }
    }
}