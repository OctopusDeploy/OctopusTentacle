using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using k8s;
using k8s.Authentication;

namespace Octopus.Tentacle.Kubernetes
{
    class LocalMachineKubernetesClientConfigProvider : IKubernetesClientConfigProvider
    {
        const string ServiceAccountTokenKeyFileName = "token";
        const string ServiceAccountRootCAKeyFileName = "ca.crt";

        public KubernetesClientConfiguration Get()
        {
#if DEBUG
            var telepresenceRoot = Environment.GetEnvironmentVariable("TELEPRESENCE_ROOT");
            if (!string.IsNullOrEmpty(telepresenceRoot))
            {
                return GetTelepresenceConfig(telepresenceRoot);
            }
            var kubeConfigEnvVar = Environment.GetEnvironmentVariable("KUBECONFIG");
            if (kubeConfigEnvVar != null && !Path.IsPathRooted(kubeConfigEnvVar))
            {
                // Path.GetFullPath doesn't work with ~, so we need to expand it manually
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

        KubernetesClientConfiguration GetTelepresenceConfig(string telepresenceRoot)
        {
            var serviceAccountPath =
                Path.Combine(new string[]
                {
                    $"{telepresenceRoot}", "var", "run", "secrets", "kubernetes.io", "serviceaccount",
                });
            var rootCAFile = Path.Combine(serviceAccountPath, ServiceAccountRootCAKeyFileName);
            var host = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            var port = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT");
            if (string.IsNullOrEmpty(host))
            {
                host = "kubernetes.default.svc";
            }

            if (string.IsNullOrEmpty(port))
            {
                port = "443";
            }

            X509Certificate2Collection certificates = new X509Certificate2Collection();
            certificates.Import(rootCAFile);
            
            var result = new KubernetesClientConfiguration
            {
                Host = new UriBuilder("https", host, Convert.ToInt32(port)).ToString(),
                TokenProvider = new TokenFileAuth(Path.Combine(serviceAccountPath, ServiceAccountTokenKeyFileName)),
                SslCaCerts = certificates,
            };
            
            var namespaceVar = Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__NAMESPACE");

            if (!string.IsNullOrEmpty(namespaceVar))
            {
                result.Namespace = namespaceVar;
            }

            return result;
        }
    }
}