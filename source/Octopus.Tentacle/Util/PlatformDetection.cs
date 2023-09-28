using System;
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Util
{
    public static class PlatformDetection
    {
        public static bool IsRunningOnNix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsRunningOnMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Indicates if the Tentacle is running inside a Kubernetes cluster.
        /// </summary>
        public static bool IsRunningInKubernetes => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")) ||
            (bool.TryParse(Environment.GetEnvironmentVariable("OCTOPUS__TENTACLE__FORCEK8S"), out var b) && b);
    }
}