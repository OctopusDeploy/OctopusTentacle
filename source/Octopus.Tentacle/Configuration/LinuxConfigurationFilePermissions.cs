#if NET
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Configuration
{
    // Only the file's owner may read it, so the service user must own it.
    [UnsupportedOSPlatform("windows")]
    class LinuxConfigurationFilePermissions : IConfigurationFilePermissions
    {
        public const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        const uint UnchangedGroup = uint.MaxValue;

        readonly ISystemLog log;

        public LinuxConfigurationFilePermissions(ISystemLog log)
        {
            this.log = log;
        }

        public void CreateRestrictedToCurrentUser(string configurationPath, string contents)
        {
            // The mode is applied as the file is created, so it is never readable by other users.
            using var stream = new FileStream(configurationPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = OwnerOnly
            });
            using var writer = new StreamWriter(stream);
            writer.Write(contents);
        }

        // The agent runs as the service user, which owns the file.
        public void RestrictToCurrentUser(string configurationPath)
        {
            if (!File.Exists(configurationPath))
            {
                return;
            }

            try
            {
                var previousMode = File.GetUnixFileMode(configurationPath);
                if (previousMode != OwnerOnly)
                {
                    File.SetUnixFileMode(configurationPath, OwnerOnly);
                    log.Info($"Restricted `{configurationPath}` from {Convert.ToString((int)previousMode, 8).PadLeft(4, '0')} to 0600");
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                log.Warn($"Unable to restrict `{configurationPath}` to its owner, so other users may be able to read it: {ex.Message}");
            }
        }

        public void RestrictToServiceUser(string configurationPath, string userName)
        {
            if (userName == "root")
            {
                return;
            }

            var error = TryRestrictToServiceUser(configurationPath, userName);
            if (error != null)
            {
                log.Warn($"Unable to restrict `{configurationPath}` to `{userName}`: {error.TrimEnd('.')}. Only the service user should be able to read and write the configuration file, so check its owner and permissions.");
            }
        }

        static string? TryRestrictToServiceUser(string configurationPath, string userName)
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
