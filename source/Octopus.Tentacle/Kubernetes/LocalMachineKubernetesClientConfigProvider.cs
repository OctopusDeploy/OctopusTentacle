using System;
using k8s;

namespace Octopus.Tentacle.Kubernetes
{
    public sealed class LocalMachineKubernetesClientConfigProvider : IKubernetesClientConfigProvider
    {
        public KubernetesClientConfiguration Get()
        {
#if DEBUG
            
            var kubeConfigEnvVar = Environment.GetEnvironmentVariable("KUBECONFIG");
            return KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigEnvVar);
#else
            throw new NotSupportedException("Local machine configuration is only supported when debugging.");
#endif
        }
    }
}