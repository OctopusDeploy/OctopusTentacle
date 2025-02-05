using System;
using k8s;

namespace Octopus.Tentacle.Kubernetes
{
    class UnimplementedKubernetesConfigProvider : IKubernetesClientConfigProvider
    {
        public KubernetesClientConfiguration Get()
        {
            throw new NotImplementedException("This provider is not implemented when running outside of the Kubernetes Agent.");
        }
    }
}