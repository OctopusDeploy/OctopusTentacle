using System;
using System.IO;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;
#if NET
using System.Diagnostics;
using System.Runtime.Versioning;
using Octopus.Tentacle.Core.Util;
#endif

namespace Octopus.Tentacle.Configuration
{
    // The config holds protected settings, so only its owner may read it. The service user must therefore own it.
    static class ConfigurationFilePermissions
    {
#if NET
        public const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
#endif

        public static void CreateEmpty(IOctopusFileSystem fileSystem, string configurationPath, string contents)
        {
#if NET
            if (!OperatingSystem.IsWindows())
            {
                using var stream = new FileStream(configurationPath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    UnixCreateMode = OwnerOnly
                });
                using var writer = new StreamWriter(stream);
                writer.Write(contents);
                return;
            }
#endif
            fileSystem.OverwriteFile(configurationPath, contents);
        }

        public static void RestrictToOwner(string? configurationPath, ISystemLog log)
        {
#if NET
            if (OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(configurationPath) || !File.Exists(configurationPath))
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
#endif
        }

        public static void RestrictToServiceUser(string? configurationPath, string? userName, ISystemLog log)
        {
#if NET
            if (OperatingSystem.IsWindows()
                || string.IsNullOrWhiteSpace(configurationPath)
                || string.IsNullOrWhiteSpace(userName)
                || userName == "root")
            {
                return;
            }

            var error = TryRestrictToServiceUser(configurationPath, userName);
            if (error != null)
            {
                log.Warn($"Unable to restrict `{configurationPath}` to `{userName}`: {error.TrimEnd('.')}. Only the service user should be able to read and write the configuration file, so check its owner and permissions.");
            }
#endif
        }

#if NET
        [UnsupportedOSPlatform("windows")]
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
                UnixFile.GiveTo(stream.SafeFileHandle, userId.Value);
                return null;
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
#endif
    }
}
