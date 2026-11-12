#if !NETFRAMEWORK
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Octopus.Tentacle.Core.Util
{
    static class UnixFile
    {
        public const uint UnchangedId = uint.MaxValue;
        const int EEXIST = 17;

        public static bool IsRoot => geteuid() == 0;

        // A symlink in the final path component is refused rather than followed, so root never changes the file it points to.
        public static SafeFileHandle OpenWithoutFollowingSymlinks(string path)
        {
            var fd = open(path, OpenFlags);
            if (fd < 0)
            {
                throw new IOException($"Unable to open `{path}` without following symbolic links: {Marshal.GetLastPInvokeErrorMessage()}");
            }
            return new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        }

        public static void ChangeOwner(SafeFileHandle handle, string path, uint userId, uint groupId)
        {
            if (fchown((int)handle.DangerousGetHandle(), userId, groupId) != 0)
            {
                throw new IOException($"Unable to change the owner of `{path}`: {Marshal.GetLastPInvokeErrorMessage()}");
            }
        }

        public static uint? UserId(string userName)
        {
            var entry = getpwnam(userName);
            if (entry == IntPtr.Zero)
            {
                return uint.TryParse(userName, out var numericId) ? numericId : null;
            }
            // pw_uid follows the pw_name and pw_passwd pointers in struct passwd on glibc, musl and macOS.
            return (uint)Marshal.ReadInt32(entry, 2 * IntPtr.Size);
        }

        // Unlike rename(2), link(2) never replaces an existing file.
        public static bool TryLinkWithoutReplacing(string existingPath, string newPath, out bool newPathExists)
        {
            var linked = link(existingPath, newPath) == 0;
            newPathExists = !linked && Marshal.GetLastPInvokeError() == EEXIST;
            return linked;
        }

        static int OpenFlags
        {
            get
            {
                if (OperatingSystem.IsMacOS())
                {
                    const int noFollow = 0x100, nonBlock = 0x4, closeOnExec = 0x1000000;
                    return noFollow | nonBlock | closeOnExec;
                }

                const int linuxNonBlock = 0x800, linuxCloseOnExec = 0x80000;
                var linuxNoFollow = RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64 ? 0x8000 : 0x20000;
                return linuxNoFollow | linuxNonBlock | linuxCloseOnExec;
            }
        }

        [DllImport("libc", SetLastError = true)]
        static extern int open(string path, int flags);

        [DllImport("libc", SetLastError = true)]
        static extern int fchown(int fd, uint owner, uint group);

        [DllImport("libc", SetLastError = true)]
        static extern IntPtr getpwnam(string name);

        [DllImport("libc")]
        static extern uint geteuid();

        [DllImport("libc", SetLastError = true)]
        static extern int link(string oldpath, string newpath);
    }
}
#endif
