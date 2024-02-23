using System;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesConfig
    {
        const string EnvVarPrefix = "OCTOPUS__K8STENTACLE";

        public static string NamespaceVariableName => $"{EnvVarPrefix}__NAMESPACE";
        public static string Namespace => GetRequiredEnvVar(NamespaceVariableName, "Unable to determine Kubernetes namespace.");
        public static string PodServiceAccountName => GetRequiredEnvVar($"{EnvVarPrefix}__PODSERVICEACCOUNTNAME", "Unable to determine Kubernetes Pod service account name.");
        public static string PodVolumeJson => GetRequiredEnvVar($"{EnvVarPrefix}__PODVOLUMEJSON", "Unable to determine Kubernetes Pod volume json.");
        public static bool ExecuteScriptsInLocalShell => bool.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__EXECUTEINLOCALSHELL"), out var executeInLocalShell) && executeInLocalShell;
        public static int PodMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__PODMONITORTIMEOUT"), out var podMonitorTimeout) ? podMonitorTimeout : 1800; //30min
        public static bool DisableAutomaticPodCleanup => bool.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__DISABLEAUTOPODCLEANUP"), out var disableAutoCleanup) && disableAutoCleanup;

        public static string HelmReleaseNameVariableName => $"{EnvVarPrefix}__HELMRELEASENAME";
        public static string HelmReleaseName => GetRequiredEnvVar(HelmReleaseNameVariableName, "Unable to determine Helm release name.");

        public static string HelmChartVersionVariableName => $"{EnvVarPrefix}__HELMCHARTVERSION";
        public static string HelmChartVersion => GetRequiredEnvVar(HelmChartVersionVariableName, "Unable to determine Helm chart version.");

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}