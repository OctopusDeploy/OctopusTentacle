using System;
using k8s;

namespace Octopus.Tentacle.Kubernetes
{
    public sealed class LocalMachineKubernetesClientConfigProvider : IKubernetesClientConfigProvider
    {
        public KubernetesClientConfiguration Get()
        {
#if DEBUG
            return KubernetesClientConfiguration.BuildConfigFromConfigFile();
#else
            throw new NotSupportedException("Local machine configuration is only supported when debugging.");
#endif
        }
    }
}