using System;
using System.IO;
using k8s;

namespace Octopus.Tentacle.Kubernetes
{
    class LocalMachineKubernetesClientConfigProvider : IKubernetesClientConfigProvider
    {
        public KubernetesClientConfiguration Get()
        {
#if DEBUG
            var kubeConfigEnvVar = Environment.GetEnvironmentVariable("KUBECONFIG");
            if (kubeConfigEnvVar != null && !Path.IsPathRooted(kubeConfigEnvVar))
            {
                if (kubeConfigEnvVar.StartsWith("~"))
                {
                    kubeConfigEnvVar = kubeConfigEnvVar
                        .Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                        .Replace("//", "/");
                }
                else
                {
                    kubeConfigEnvVar = Path.GetFullPath(kubeConfigEnvVar);
                }
            }
            return KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigEnvVar);
#else
            throw new NotSupportedException("Local machine configuration is only supported when debugging.");
#endif
        }
    }
}