using System;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesNamespace
    {
        public static string Value => Environment.GetEnvironmentVariable("OCTOPUS__TENTACLE__K8SNAMESPACE")
            ?? throw new InvalidOperationException("Unable to determine Kubernetes namespace. There must be an environment variable 'OCTOPUS__TENTACLE__K8SNAMESPACE' defined.");
    }
}