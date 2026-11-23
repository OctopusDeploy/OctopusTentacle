using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Octopus.Tentacle.Util
{
    public static class PlatformDetection
    {
        public static bool IsRunningOnNix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#if NET
        [SupportedOSPlatformGuard("windows")]
#endif
        public static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsRunningOnMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }
}