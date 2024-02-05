﻿using System;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesConfig
    {
        const string EnvVarPrefix = "OCTOPUS__K8STENTACLE";

        public static string Namespace => GetRequiredEnvVar($"{EnvVarPrefix}__NAMESPACE", "Unable to determine Kubernetes namespace.");
        public static string JobServiceAccountName => GetRequiredEnvVar($"{EnvVarPrefix}__JOBSERVICEACCOUNTNAME", "Unable to determine Kubernetes Job service account name.");
        public static string JobVolumeYaml => GetRequiredEnvVar($"{EnvVarPrefix}__JOBVOLUMEYAML", "Unable to determine Kubernetes Job volume yaml.");
        public static bool UseJobs => bool.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__USEJOBS"), out var useJobs) && useJobs;
        public static int JobTtlSeconds => int.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__JOBTTL"), out var jobTtl) ? jobTtl : 60; //Default 1min
        public static int JobMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__JOBMONITORTIMEOUT"), out var jobMonitorTimeout) ? jobMonitorTimeout : 1800; //30min

        public static string HelmReleaseName => GetRequiredEnvVar($"{EnvVarPrefix}__HELMRELEASENAME", "Unable to determine Helm release name.");
        public static string HelmChartVersion => GetRequiredEnvVar($"{EnvVarPrefix}__HELMCHARTVERSION", "Unable to determine Helm chart version.");

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}