using System;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Util
{
    public static class KubernetesSupportDetection
    {
        /// <summary>
        /// Indicates if the Tentacle is running inside a Kubernetes cluster as the Kubernetes Agent. This is done by checking if the namespace environment variable is set
        /// </summary>
        public static bool IsRunningAsKubernetesAgent => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(KubernetesConfig.NamespaceVariableName));
    }
}