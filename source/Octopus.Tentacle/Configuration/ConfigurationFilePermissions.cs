#if NET
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Octopus.Tentacle.Configuration
{
    // The config holds protected settings, so only its owner may read it. The service user must therefore own it.
    [UnsupportedOSPlatform("windows")]
    static class ConfigurationFilePermissions
    {
        public const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        const uint UnchangedGroup = uint.MaxValue;

        // Only the agent calls this, as the service user, so it can never lock that user out of the config.
        public static void RestrictToOwner(string configurationPath)
            => File.SetUnixFileMode(configurationPath, OwnerOnly);

        public static string? TryRestrictToServiceUser(string configurationPath, string userName)
        {
            try
            {
                if (new FileInfo(configurationPath).LinkTarget != null)
                    return $"`{configurationPath}` is a symbolic link.";

                var userId = UserId(userName);
                if (userId == null)
                    return $"Unable to find the user `{userName}`.";

                using var stream = new FileStream(configurationPath, FileMode.Open, FileAccess.Read);
                File.SetUnixFileMode(stream.SafeFileHandle, OwnerOnly);
                return fchown((int)stream.SafeFileHandle.DangerousGetHandle(), userId.Value, UnchangedGroup) == 0
                    ? null
                    : Marshal.GetLastPInvokeErrorMessage();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        static uint? UserId(string userName)
        {
            if (userName.StartsWith("-", StringComparison.Ordinal))
                return null;

            var startInfo = new ProcessStartInfo("id") { UseShellExecute = false, RedirectStandardOutput = true };
            startInfo.ArgumentList.Add("-u");
            startInfo.ArgumentList.Add(userName);
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && uint.TryParse(output.Trim(), out var userId) ? userId : null;
        }

        [DllImport("libc", SetLastError = true)]
        static extern int fchown(int fd, uint owner, uint group);
    }
}
#endif
