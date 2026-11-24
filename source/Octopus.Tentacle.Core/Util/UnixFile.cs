#if !NETFRAMEWORK
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Octopus.Tentacle.Core.Util
{
    [UnsupportedOSPlatform("windows")]
    public static class UnixFile
    {
        const uint UnchangedGroup = uint.MaxValue;
        const int FileExistsError = 17;

        // Unlike rename(2), link(2) never replaces an existing file.
        public static bool TryLinkWithoutReplacing(string existingPath, string newPath)
        {
            if (link(existingPath, newPath) == 0)
            {
                return true;
            }

            return Marshal.GetLastPInvokeError() == FileExistsError
                ? false
                : throw new IOException($"Unable to create `{newPath}`: {Marshal.GetLastPInvokeErrorMessage()}");
        }

        public static uint OwnerOf(string path)
        {
            return SystemNative_Stat(path, out var status) == 0
                ? status.Uid
                : throw new IOException($"Unable to find the owner of `{path}`: {Marshal.GetLastPInvokeErrorMessage()}");
        }

        public static void SetOwnerIfRunningAsRoot(uint owner, string path)
        {
            if (geteuid() != 0 || OwnerOf(path) == owner)
            {
                return;
            }

            if (lchown(path, owner, UnchangedGroup) != 0)
            {
                throw new IOException($"Unable to change the owner of `{path}`: {Marshal.GetLastPInvokeErrorMessage()}");
            }
        }

        public static void GiveTo(SafeFileHandle file, uint owner)
        {
            if (fchown(file, owner, UnchangedGroup) != 0)
            {
                throw new IOException(Marshal.GetLastPInvokeErrorMessage());
            }
        }

        [DllImport("libc", SetLastError = true)]
        static extern int link(string oldpath, string newpath);

        [DllImport("libc")]
        static extern uint geteuid();

        [DllImport("libc", SetLastError = true)]
        static extern int lchown(string path, uint owner, uint group);

        [DllImport("libc", SetLastError = true)]
        static extern int fchown(SafeFileHandle fd, uint owner, uint group);

        // A private runtime shim: safe as Linux builds ship their own runtime, and test-linux-package.sh catches a layout change.
        [DllImport("libSystem.Native", SetLastError = true)]
        static extern int SystemNative_Stat(string path, out FileStatus output);

        [StructLayout(LayoutKind.Sequential)]
        struct FileStatus
        {
            public int Flags;
            public int Mode;
            public uint Uid;
            public uint Gid;
            public long Size;
            public long ATime;
            public long ATimeNsec;
            public long MTime;
            public long MTimeNsec;
            public long CTime;
            public long CTimeNsec;
            public long BirthTime;
            public long BirthTimeNsec;
            public long Dev;
            public long RDev;
            public long Ino;
            public uint UserFlags;
        }
    }
}
#endif
