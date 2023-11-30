using System;
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Util
{
    public static class PlatformDetection
    {
        public static bool IsRunningOnNix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsRunningOnMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static class Kubernetes
        {
            /// <summary>
            /// Indicates if the Tentacle is running inside a Kubernetes cluster.
            /// </summary>
            public static bool IsRunningInKubernetes => bool.TryParse(Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__FORCE"), out var b) && b;
        }
    }
}