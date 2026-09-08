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
        /// Whether this is a Linux host on which no OpenSSL 1.x runtime can be loaded.
        /// <para>
        /// Older Tentacle binaries are .NET Core 3.1 builds, which load OpenSSL 1.x during
        /// start-up and abort with "No usable version of libssl was found" when they cannot
        /// find one. Distributions from Ubuntu 22.04 onwards ship only OpenSSL 3, so this
        /// has to be probed rather than assumed.
        /// </para>
        /// False on every other platform - not because OpenSSL 1.x is present there, but
        /// because nothing else loads libssl by soname this way, so the probe is not run
        /// and no claim is made either way.
        /// </summary>
        public static bool IsLinuxWithoutOpenSsl1x => LinuxWithoutOpenSsl1x.Value;

        // Lazy so that the probe only loads a library when something actually asks about
        // OpenSSL, rather than on the first touch of any member of this class.
        static readonly Lazy<bool> LinuxWithoutOpenSsl1x = new(() => IsRunningOnNix && !CanDlopenOpenSsl1x());

#if NETFRAMEWORK
        // NativeLibrary was introduced in .NET Core 3.0, so it does not exist on net48.
        // This project only builds net48 on Windows (see the TargetFrameworks conditions
        // in the csproj), where IsRunningOnNix short-circuits before this is ever called.
        // It answers "no" rather than "yes" so that any future TFM landing here excludes
        // the affected Tentacle versions instead of scheduling tests that abort.
        static bool CanDlopenOpenSsl1x() => false;
#else
        static bool CanDlopenOpenSsl1x()
        {
            // dlopen by soname, so the dynamic linker's own search path is used rather
            // than a guess at where the distribution puts its libraries. The list and its
            // order mirror OpenLibrary() in .NET Core 3.1's opensslshim.c, so the probe
            // answers the same question the Tentacle's own start-up asks:
            // https://github.com/dotnet/corefx/blob/release/3.1/src/Native/Unix/System.Security.Cryptography.Native/opensslshim.c
            // The one deliberate omission is the CLR_OPENSSL_VERSION_OVERRIDE env var,
            // which the shim tries first and nothing in this repo sets.
            foreach (var soname in new[]
                     {
                         "libssl.so.1.1",
                         "libssl.so.1.0.2", // Debian 9 bumped its soname when it dropped SSLv3
                         "libssl.so.1.0.0",
                         "libssl.so.10" // Fedora-derived distros
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
