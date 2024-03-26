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

        public static int PodMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__PODMONITORTIMEOUT"), out var podMonitorTimeout) ? podMonitorTimeout : 10*60; //10min

        public static TimeSpan PodsConsideredOrphanedAfterTimeSpan => TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__PODSCONSIDEREDORPHANEDAFTERMINUTES"), out var podsConsideredOrphanedAfterTimeSpan) ? podsConsideredOrphanedAfterTimeSpan : 10);
        public static bool DisableAutomaticPodCleanup => bool.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__DISABLEAUTOPODCLEANUP"), out var disableAutoCleanup) && disableAutoCleanup;

        public static string HelmReleaseNameVariableName => $"{EnvVarPrefix}__HELMRELEASENAME";
        public static string HelmReleaseName => GetRequiredEnvVar(HelmReleaseNameVariableName, "Unable to determine Helm release name.");

        public static string HelmChartVersionVariableName => $"{EnvVarPrefix}__HELMCHARTVERSION";
        public static string HelmChartVersion => GetRequiredEnvVar(HelmChartVersionVariableName, "Unable to determine Helm chart version.");

        public static string BootstrapRunnerExecutablePath => GetRequiredEnvVar("BOOTSTRAPRUNNEREXECUTABLEPATH", "Unable to determine Bootstrap Runner Executable Path");

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}