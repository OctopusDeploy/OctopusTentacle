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
        const int NotPermittedError = 1;
        const int FileExistsError = 17;
        const int NotSupportedError = 95;

        // Unlike rename(2), link(2) never replaces an existing file.
        public static bool TryLinkWithoutReplacing(string existingPath, string newPath)
        {
            if (link(existingPath, newPath) == 0)
            {
                return true;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error == FileExistsError)
            {
                return false;
            }

            return error == NotPermittedError || error == NotSupportedError
                ? TryCopyWithoutReplacing(existingPath, newPath)
                : throw new IOException($"Unable to create `{newPath}`: {Marshal.GetLastPInvokeErrorMessage()}");
        }

        // For file systems without hard links, such as SMB shares. Another process may briefly see the new file empty.
        static bool TryCopyWithoutReplacing(string existingPath, string newPath)
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
            };

            FileStream destination;
            try
            {
                destination = new FileStream(newPath, options);
            }
            catch (IOException) when (File.Exists(newPath))
            {
                return false;
            }

            try
            {
                using (destination)
                {
                    using var source = File.OpenRead(existingPath);
                    source.CopyTo(destination);
                    destination.Flush(flushToDisk: true);
                }

                return true;
            }
            catch
            {
                File.Delete(newPath);
                throw;
            }
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
        
        // This is an internal shim that could change in the future, although it hasn't changed for years.
        // This would only break when we update .net, which we do by commiting to this repo. Thus tests will catch the
        // issue which would allow us to fix it.
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
