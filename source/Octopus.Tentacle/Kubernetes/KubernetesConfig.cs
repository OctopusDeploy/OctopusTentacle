using System;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesConfig
    {
        public static string Namespace => Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__NAMESPACE")
            ?? throw new InvalidOperationException("Unable to determine Kubernetes namespace. An environment variable 'OCTOPUS__K8STENTACLE__NAMESPACE' must be defined.");

        public static bool UseJobs => bool.TryParse(Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__USEJOBS"), out var useJobs) && useJobs;

        public static string JobServiceAccountName => Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__JOBSERVICEACCOUNTNAME")
            ?? throw new InvalidOperationException("Unable to determine Kubernetes Job service account name. An environment variable 'OCTOPUS__K8STENTACLE__JOBSERVICEACCOUNTNAME' must be defined.");

        public static string JobVolumeYaml => Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__JOBVOLUMEYAML")
            ?? throw new InvalidOperationException("Unable to determine Kubernetes Job volume yaml. An environment variable 'OCTOPUS__K8STENTACLE__JOBVOLUMEYAML' must be defined.");
    }
}