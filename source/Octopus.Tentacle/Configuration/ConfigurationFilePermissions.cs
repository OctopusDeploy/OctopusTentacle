#if NET
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Octopus.Tentacle.Configuration
{
    // The config holds protected settings, so only its owner may read it. The service user must therefore own it.
    [UnsupportedOSPlatform("windows")]
    static class ConfigurationFilePermissions
    {
        public const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        const uint UnchangedGroup = uint.MaxValue;

        public static void RestrictToOwner(string configurationPath)
            => File.SetUnixFileMode(configurationPath, OwnerOnly);

        public static string? TryRestrictToServiceUser(string configurationPath, string userName)
        {
            try
            {
                if (new FileInfo(configurationPath).LinkTarget != null)
                {
                    return $"`{configurationPath}` is a symbolic link.";
                }

                var userId = GetUserId(userName);
                if (userId == null)
                {
                    return $"Unable to find the user `{userName}`.";
                }

                using var stream = new FileStream(configurationPath, FileMode.Open, FileAccess.Read);
                File.SetUnixFileMode(stream.SafeFileHandle, OwnerOnly);
                return fchown(stream.SafeFileHandle, userId.Value, UnchangedGroup) == 0
                    ? null
                    : Marshal.GetLastPInvokeErrorMessage();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        static uint? GetUserId(string userName)
        {
            if (userName.StartsWith("-", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                var startInfo = new ProcessStartInfo("id") { UseShellExecute = false, RedirectStandardOutput = true };
                startInfo.ArgumentList.Add("-u");
                startInfo.ArgumentList.Add(userName);
                using var process = Process.Start(startInfo)!;
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 && uint.TryParse(output.Trim(), out var userId) ? userId : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [DllImport("libc", SetLastError = true)]
        static extern int fchown(SafeFileHandle fd, uint owner, uint group);
    }
}
#endif
