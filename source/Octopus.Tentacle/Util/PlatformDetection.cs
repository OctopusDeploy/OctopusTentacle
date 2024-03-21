using System;
using System.Runtime.InteropServices;
using Octopus.Tentacle.Kubernetes;

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
            /// Indicates if the Tentacle is running inside a Kubernetes cluster as the Kubernetes Agent. This is done by checking if the namespace environment variable is set
            /// </summary>
            public static bool IsRunningAsKubernetesAgent => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(KubernetesConfig.NamespaceVariableName));
        }
    }
}