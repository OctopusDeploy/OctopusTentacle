#if !NETFRAMEWORK
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Octopus.Tentacle.Core.Util
{
    // .NET has no managed API for file owners, so owners are read with POSIX tools and set on an open handle.
    static class UnixFile
    {
        public const uint UnchangedId = uint.MaxValue;

        public static void ChangeOwner(SafeFileHandle handle, string path, uint userId, uint groupId)
        {
            if (fchown((int)handle.DangerousGetHandle(), userId, groupId) != 0)
            {
                throw new IOException($"Unable to change the owner of `{path}`: {Marshal.GetLastPInvokeErrorMessage()}");
            }
        }

        // `ls -ln` reports a symlink's own owner, and its output is POSIX, so it is parsed the same on GNU, BSD and BusyBox.
        public static (uint UserId, uint GroupId) Owner(string path)
        {
            var fields = Run("ls", "-ldn", "--", path)?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields == null || fields.Length < 5 || !uint.TryParse(fields[2], out var userId) || !uint.TryParse(fields[3], out var groupId))
            {
                throw new IOException($"Unable to read the owner of `{path}`.");
            }
            return (userId, groupId);
        }

        public static uint? UserId(string userName)
            => !userName.StartsWith("-", StringComparison.Ordinal) && uint.TryParse(Run("id", "-u", userName)?.Trim(), out var userId) ? userId : null;

        // Unlike rename(2), link(2) never replaces an existing file.
        public static bool TryLinkWithoutReplacing(string existingPath, string newPath)
            => link(existingPath, newPath) == 0;

        static string? Run(string program, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo(program)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            errors.Wait();
            return process.ExitCode == 0 ? output : null;
        }

        [DllImport("libc", SetLastError = true)]
        static extern int fchown(int fd, uint owner, uint group);

        [DllImport("libc", SetLastError = true)]
        static extern int link(string oldpath, string newpath);
    }
}
#endif
