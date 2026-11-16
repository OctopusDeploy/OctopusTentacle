#if !NETFRAMEWORK
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Core.Util
{
    static class UnixFile
    {
        // Unlike rename(2), link(2) never replaces an existing file.
        public static bool TryLinkWithoutReplacing(string existingPath, string newPath)
            => link(existingPath, newPath) == 0;

        [DllImport("libc", SetLastError = true)]
        static extern int link(string oldpath, string newpath);
    }
}
#endif
