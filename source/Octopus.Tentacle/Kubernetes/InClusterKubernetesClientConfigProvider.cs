using System;
using k8s;

namespace Octopus.Tentacle.Kubernetes
{
    class InClusterKubernetesClientConfigProvider : IKubernetesClientConfigProvider
    {
        public KubernetesClientConfiguration Get()
        {
            return KubernetesClientConfiguration.InClusterConfig();
        }
    }
}