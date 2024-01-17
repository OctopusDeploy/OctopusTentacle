using System;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesConfig
    {
        public static bool IsRunningInKubernetesCluster => !Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST").IsNullOrEmpty();
        public static string Namespace => GetRequiredEnvVar("OCTOPUS__K8STENTACLE__NAMESPACE", "Unable to determine Kubernetes namespace.");
        public static string JobServiceAccountName => GetRequiredEnvVar("OCTOPUS__K8STENTACLE__JOBSERVICEACCOUNTNAME", "Unable to determine Kubernetes Job service account name.");
        public static string JobVolumeYaml => GetRequiredEnvVar("OCTOPUS__K8STENTACLE__JOBVOLUMEYAML", "Unable to determine Kubernetes Job volume yaml.");
        public static bool UseJobs => bool.TryParse(Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__USEJOBS"), out var useJobs) && useJobs;
        public static int JobTtlSeconds => int.TryParse(Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__JOBTTL"), out var jobTtl) ? jobTtl : 60; //Default 1min

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}