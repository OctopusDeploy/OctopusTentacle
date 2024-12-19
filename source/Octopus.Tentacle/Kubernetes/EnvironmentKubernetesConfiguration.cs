using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class EnvironmentKubernetesConfiguration : IKubernetesConfiguration
    {
        public static class VariableNames
        {
            const string EnvVarPrefix = "OCTOPUS__K8STENTACLE";
            
            public const string Namespace = $"{EnvVarPrefix}__NAMESPACE";

            public const string BoolstrapRunnerExecutablePath = "BOOTSTRAPRUNNEREXECUTABLEPATH";

            public const string PersistentVolumeSizeBytes = $"{EnvVarPrefix}__PERSISTENTVOLUMETOTALBYTES";
            public const string PersistentVolumeFreeBytes = $"{EnvVarPrefix}__PERSISTENTVOLUMEFREEBYTES";

            public const string HelmReleaseName = $"{EnvVarPrefix}__HELMRELEASENAME";
            public const string HelmChartVersion = $"{EnvVarPrefix}__HELMCHARTVERSION";
            
            public const string ServerCommsAddress = "ServerCommsAddress";
            public const string ServerCommsAddresses = "ServerCommsAddresses";

            public const string ScriptPodServiceAccountName = $"{EnvVarPrefix}__SCRIPTPODSERVICEACCOUNTNAME";
            public const string ScriptPodImagePullSecretNames = $"{EnvVarPrefix}__PODIMAGEPULLSECRETNAMES";
            public const string ScriptPodVolumeClaimName = $"{EnvVarPrefix}__PODVOLUMECLAIMNAME";

            public const string ScriptPodResourceJson = $"{EnvVarPrefix}__PODRESOURCEJSON";
            public const string ScriptPodAffinityJson = $"{EnvVarPrefix}__PODAFFINITYJSON";
            public const string ScriptPodTolerationsJson = $"{EnvVarPrefix}__PODTOLERATIONSJSON";
            public const string ScriptPodSecurityContextJson = $"{EnvVarPrefix}__PODSECURITYCONTEXTJSON";
            

            public const string ScriptPodContainerImage = $"{EnvVarPrefix}__SCRIPTPODIMAGE";
            public const string ScriptPodContainerImageTag = $"{EnvVarPrefix}__SCRIPTPODIMAGETAG";
            public const string ScriptPodPullPolicy = $"{EnvVarPrefix}__SCRIPTPODPULLPOLICY";


            public const string ScriptPodProxiesSecretName = $"{EnvVarPrefix}__PODPROXIESSECRETNAME";
            
            
            public const string NfsWatchdogImage = $"{EnvVarPrefix}__NFSWATCHDOGIMAGE";
            public const string ScriptPodMonitorTimeout = $"{EnvVarPrefix}__PODMONITORTIMEOUT";
            public const string ScriptPodConsideredOrphanedAfterTimeSpan = $"{EnvVarPrefix}__PODSCONSIDEREDORPHANEDAFTERMINUTES";

            public const string DisableAutomaticPodCleanup = $"{EnvVarPrefix}__DISABLEAUTOPODCLEANUP";
            public const string DisablePodEventsInTaskLog = $"{EnvVarPrefix}__DISABLEPODEVENTSINTASKLOG";

            public const string PersistentVolumeSize = $"{EnvVarPrefix}__PERSISTENTVOLUMESIZE";

            public const string IsMetricsEnabled = $"{EnvVarPrefix}__ENABLEMETRICSCAPTURE";
        }

        public string Namespace => GetRequiredEnvVar(VariableNames.Namespace, "Unable to determine Kubernetes namespace.");
        public string BootstrapRunnerExecutablePath => GetRequiredEnvVar(VariableNames.BoolstrapRunnerExecutablePath, "Unable to determine Bootstrap Runner Executable Path");
        public string ScriptPodServiceAccountName => GetRequiredEnvVar(VariableNames.ScriptPodServiceAccountName, "Unable to determine Kubernetes Pod service account name.");

        public IEnumerable<string?> ScriptPodImagePullSecretNames => Environment.GetEnvironmentVariable(VariableNames.ScriptPodImagePullSecretNames)
            ?.Split(',')
            .Select(str => str.Trim())
            .WhereNotNullOrWhiteSpace()
            .ToArray() ?? [];

        public string ScriptPodVolumeClaimName => GetRequiredEnvVar(VariableNames.ScriptPodVolumeClaimName, "Unable to determine Kubernetes Pod persistent volume claim name.");
        public string? ScriptPodResourceJson => Environment.GetEnvironmentVariable(VariableNames.ScriptPodResourceJson);

        public string? ScriptPodContainerImage => Environment.GetEnvironmentVariable(VariableNames.ScriptPodContainerImage);
        public string ScriptPodContainerImageTag => Environment.GetEnvironmentVariable(VariableNames.ScriptPodContainerImageTag) ?? "latest";
        public string? ScriptPodPullPolicy => Environment.GetEnvironmentVariable(VariableNames.ScriptPodPullPolicy);
        public string? NfsWatchdogImage => Environment.GetEnvironmentVariable(VariableNames.NfsWatchdogImage);
        
        public string HelmReleaseName => GetRequiredEnvVar(VariableNames.HelmReleaseName, "Unable to determine Helm release name.");
        public string HelmChartVersion => GetRequiredEnvVar(VariableNames.HelmChartVersion, "Unable to determine Helm chart version.");

        public string[] ServerCommsAddresses
        {
            get
            {
                var addresses = new List<string>();
                if (Environment.GetEnvironmentVariable(VariableNames.ServerCommsAddress) is { Length: > 0 } addressString)
                {
                    addresses.Add(addressString);
                }

                if (Environment.GetEnvironmentVariable(VariableNames.ServerCommsAddresses) is { } addressesString)
                {
                    addresses.AddRange(addressesString.Split(',').Where(a => !a.IsNullOrEmpty()));
                }

                return addresses.ToArray();
            }
        }

        public int? PodMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable(VariableNames.ScriptPodMonitorTimeout), out var podMonitorTimeout) ? podMonitorTimeout : 10 * 60; //10min
        public TimeSpan PodsConsideredOrphanedAfterTimeSpan => TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable(VariableNames.ScriptPodConsideredOrphanedAfterTimeSpan), out var podsConsideredOrphanedAfterTimeSpan) ? podsConsideredOrphanedAfterTimeSpan : 10);
        public bool DisableAutomaticPodCleanup => bool.TryParse(Environment.GetEnvironmentVariable(VariableNames.DisableAutomaticPodCleanup), out var disableAutoCleanup) && disableAutoCleanup;
        public bool DisablePodEventsInTaskLog => bool.TryParse(Environment.GetEnvironmentVariable(VariableNames.DisablePodEventsInTaskLog), out var disable) && disable;
        public string PersistentVolumeSize => GetRequiredEnvVar(VariableNames.PersistentVolumeSize, "Unable to determine Persistent Volume Size");

        public bool IsMetricsEnabled => !bool.TryParse(Environment.GetEnvironmentVariable(VariableNames.IsMetricsEnabled), out var enableMetrics) || enableMetrics;
        public string? PodAffinityJson => Environment.GetEnvironmentVariable(VariableNames.ScriptPodAffinityJson);
        public string? PodTolerationsJson => Environment.GetEnvironmentVariable(VariableNames.ScriptPodTolerationsJson);
        public string? PodSecurityContextJson => Environment.GetEnvironmentVariable(VariableNames.ScriptPodSecurityContextJson);
        public string? ScriptPodProxiesSecretName => Environment.GetEnvironmentVariable(VariableNames.ScriptPodProxiesSecretName);

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}