using System;
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.CommonTestUtils
{
    public static class PlatformDetection
    {
        public static bool IsRunningOnNix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsRunningOnMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Whether an OpenSSL 1.x runtime can be loaded on this host.
        /// <para>
        /// Older Tentacle binaries are .NET Core 3.1 builds, which load OpenSSL 1.x during
        /// start-up and abort with "No usable version of libssl was found" when they cannot
        /// find one. Distributions from Ubuntu 22.04 onwards ship only OpenSSL 3, so this
        /// has to be probed rather than assumed.
        /// </para>
        /// Only meaningful on Linux; returns true elsewhere, since no other platform loads
        /// libssl by soname this way.
        /// </summary>
        public static bool CanLoadOpenSsl1x { get; } = DetectOpenSsl1x();

#if NET8_0_OR_GREATER
        static bool DetectOpenSsl1x()
        {
            if (!IsRunningOnNix) return true;

            // dlopen by soname, so the dynamic linker's own search path is used rather
            // than a guess at where the distribution puts its libraries. This is the
            // same resolution the .NET Core 3.1 runtime performs.
            foreach (var soname in new[] { "libssl.so.1.1", "libssl.so.1.0.0", "libssl.so.10" })
            {
                if (!NativeLibrary.TryLoad(soname, out var handle)) continue;

                NativeLibrary.Free(handle);
                return true;
            }

            return false;
        }
#else
        // NativeLibrary was introduced in .NET Core 3.0, so it does not exist on net48.
        // This project only builds net48 on Windows (see the TargetFrameworks conditions
        // in the csproj), and Windows never loads libssl by soname, so the answer there
        // is the same "true" the runtime check above would give.
        static bool DetectOpenSsl1x() => true;
#endif
    }
}