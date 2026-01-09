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
           
            var kubeConfigEnvVar = GetKubeConfigPath();
            
            var contextEnvVar = Environment.GetEnvironmentVariable(KubernetesConfig.KubeContextVariableName);
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigEnvVar, contextEnvVar);

            var namespaceEnvVar = Environment.GetEnvironmentVariable(KubernetesConfig.NamespaceVariableName);
            if (!string.IsNullOrEmpty(namespaceEnvVar))
            {
                config.Namespace = namespaceEnvVar;
            }

            return config;
#else
            throw new NotSupportedException("Local machine configuration is only supported when debugging.");
#endif
        }

        static string? GetKubeConfigPath()
        {
            var kubeConfigEnvVar = Environment.GetEnvironmentVariable("KUBECONFIG");
            if (string.IsNullOrEmpty(kubeConfigEnvVar) || Path.IsPathRooted(kubeConfigEnvVar)) return kubeConfigEnvVar;

            // Path.GetFullPath doesn't work with ~, so we need to expand it manually
            if (kubeConfigEnvVar.StartsWith("~"))
            {
                return kubeConfigEnvVar
                    .Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                    .Replace("//", "/");
            }

            return Path.GetFullPath(kubeConfigEnvVar);
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
            
            var namespaceVar = Environment.GetEnvironmentVariable(KubernetesConfig.NamespaceVariableName);

            if (!string.IsNullOrEmpty(namespaceVar))
            {
                result.Namespace = namespaceVar;
            }

            return result;
        }
    }
}