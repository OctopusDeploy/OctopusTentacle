using System;
using System.ComponentModel;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
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
                    var username = name ?? new string(Guid.NewGuid().ToString("N").ToLowerInvariant().Where(char.IsLetter).ToArray());
                    Console.WriteLine($"Trying to create a temporary Windows User Account on the local machine called '{principal.Name}'...");

                    principalContext = new PrincipalContext(contextType);
                    {
                        principal = new UserPrincipal(principalContext);
                        principal.Name = username;
                        principal.SetPassword(password);
                        principal.Save();
                        this.password = password;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create the temporary Windows User Account called '{principal.Name}': {ex.Message}");
                    if (attempts >= 5) throw;
                }
            }
        }

        public SecurityIdentifier Sid => principal.Sid;
        public string NTAccountName => principal.Sid.Translate(typeof(NTAccount)).ToString();
        public string DomainName => NTAccountName.Split(new[] {'\\'}, 2)[0];
        public string UserName => NTAccountName.Split(new[] {'\\'}, 2)[1];
        public string SamAccountName => principal.SamAccountName;
        public string Password => password;
        public NetworkCredential GetCredential() => new NetworkCredential(UserName, Password, DomainName);

        public void Dispose()
        {
            TryDeleteProfile();
            principal.Delete();
            principal.Dispose();
            principalContext.Dispose();
        }

        public override string ToString()
        {
            return NTAccountName;
        }

        [DllImport("userenv.dll", SetLastError = true)]
        public static extern bool DeleteProfile(string sidString, string profilePath, string computerName);

        void TryDeleteProfile()
        {
            try
            {
                Console.WriteLine($"Deleting profile for {NTAccountName}...");
                Invoke(() => DeleteProfile(Sid.Value, null, null), $"Failed to delete the profile for '{NTAccountName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public static bool Invoke(Func<bool> nativeMethod, string failureDescription)
        {
            try
            {
                return nativeMethod() ? true : throw new Win32Exception();
            }
            catch (Win32Exception ex)
            {
                throw new Exception($"{failureDescription}: {ex.Message}", ex);
            }
        }
    }
}