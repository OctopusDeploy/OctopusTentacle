using System.Runtime.InteropServices;

namespace Octopus.Tentacle.CommonTestUtils
{
    public static class PlatformDetection
    {
        public static bool IsRunningOnNix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsRunningOnMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsLinuxWithoutOpenSsl1x => LinuxWithoutOpenSsl1x.Value;

        // Lazy so that the probe only loads a library when something actually asks about
        // OpenSSL, rather than on the first touch of any member of this class.
        static readonly Lazy<bool> LinuxWithoutOpenSsl1x = new(() => IsRunningOnNix && !CanDlopenOpenSsl1x());

// NativeLibrary was introduced in .NET Core 3.0, so it does not exist on net48.
#if NETFRAMEWORK
        static bool CanDlopenOpenSsl1x() => false;
#else
        static bool CanDlopenOpenSsl1x()
        {
            // The list and its order mirror OpenLibrary() in .NET Core 3.1's opensslshim.c.
            foreach (var soname in new[]
                     {
                         "libssl.so.1.1",
                         "libssl.so.1.0.2",
                         "libssl.so.1.0.0",
                         "libssl.so.10"
                     })
            {
                if (!NativeLibrary.TryLoad(soname, out var handle)) continue;

                NativeLibrary.Free(handle);
                return true;
            }

            return false;
        }
#endif
    }
}
