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
            
            public static readonly string Namespace = $"{EnvVarPrefix}__NAMESPACE";

            public static readonly string BootstrapRunnerExecutablePath = "BOOTSTRAPRUNNEREXECUTABLEPATH";

            public static readonly string PersistentVolumeSizeBytes = $"{EnvVarPrefix}__PERSISTENTVOLUMETOTALBYTES";
            public static readonly string PersistentVolumeFreeBytes = $"{EnvVarPrefix}__PERSISTENTVOLUMEFREEBYTES";

            public static readonly string HelmReleaseName = $"{EnvVarPrefix}__HELMRELEASENAME";
            public static readonly string HelmChartVersion = $"{EnvVarPrefix}__HELMCHARTVERSION";
            
            public static readonly string ServerCommsAddress = "ServerCommsAddress";
            public static readonly string ServerCommsAddresses = "ServerCommsAddresses";

            public static readonly string ScriptPodServiceAccountName = $"{EnvVarPrefix}__SCRIPTPODSERVICEACCOUNTNAME";
            public static readonly string ScriptPodImagePullSecretNames = $"{EnvVarPrefix}__PODIMAGEPULLSECRETNAMES";
            public static readonly string ScriptPodVolumeClaimName = $"{EnvVarPrefix}__PODVOLUMECLAIMNAME";

            public static readonly string ScriptPodResourceJson = $"{EnvVarPrefix}__PODRESOURCEJSON";
            public static readonly string ScriptPodAffinityJson = $"{EnvVarPrefix}__PODAFFINITYJSON";
            public static readonly string ScriptPodTolerationsJson = $"{EnvVarPrefix}__PODTOLERATIONSJSON";
            public static readonly string ScriptPodSecurityContextJson = $"{EnvVarPrefix}__PODSECURITYCONTEXTJSON";
            

            public static readonly string ScriptPodContainerImage = $"{EnvVarPrefix}__SCRIPTPODIMAGE";
            public static readonly string ScriptPodContainerImageTag = $"{EnvVarPrefix}__SCRIPTPODIMAGETAG";
            public static readonly string ScriptPodPullPolicy = $"{EnvVarPrefix}__SCRIPTPODPULLPOLICY";


            public static readonly string ScriptPodProxiesSecretName = $"{EnvVarPrefix}__PODPROXIESSECRETNAME";
            
            
            public static readonly string NfsWatchdogImage = $"{EnvVarPrefix}__NFSWATCHDOGIMAGE";
            public static readonly string ScriptPodMonitorTimeout = $"{EnvVarPrefix}__PODMONITORTIMEOUT";
            public static readonly string ScriptPodConsideredOrphanedAfterTimeSpan = $"{EnvVarPrefix}__PODSCONSIDEREDORPHANEDAFTERMINUTES";

            public static readonly string DisableAutomaticPodCleanup = $"{EnvVarPrefix}__DISABLEAUTOPODCLEANUP";
            public static readonly string DisablePodEventsInTaskLog = $"{EnvVarPrefix}__DISABLEPODEVENTSINTASKLOG";

            public static readonly string PersistentVolumeSize = $"{EnvVarPrefix}__PERSISTENTVOLUMESIZE";

            public static readonly string IsMetricsEnabled = $"{EnvVarPrefix}__ENABLEMETRICSCAPTURE";
        }

        public string Namespace => GetRequiredEnvVar(VariableNames.Namespace, "Unable to determine Kubernetes namespace.");
        public string BootstrapRunnerExecutablePath => GetRequiredEnvVar(VariableNames.BootstrapRunnerExecutablePath, "Unable to determine Bootstrap Runner Executable Path");
        public string ScriptPodServiceAccountName => GetRequiredEnvVar(VariableNames.ScriptPodServiceAccountName, "Unable to determine Kubernetes Pod service account name.");

        public IEnumerable<string?> ScriptPodImagePullSecretNames => Environment.GetEnvironmentVariable(VariableNames.ScriptPodImagePullSecretNames)
            ?.Split(',')
            .Select(str => str.Trim())
            .WhereNotNullOrWhiteSpace()
            .ToArray() ?? Array.Empty<string>();

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

        public int? ScriptPodMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable(VariableNames.ScriptPodMonitorTimeout), out var podMonitorTimeout) ? podMonitorTimeout : 10 * 60; //10min
        public TimeSpan ScriptPodConsideredOrphanedAfterTimeSpan => TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable(VariableNames.ScriptPodConsideredOrphanedAfterTimeSpan), out var podsConsideredOrphanedAfterTimeSpan) ? podsConsideredOrphanedAfterTimeSpan : 10);
        public bool DisableAutomaticPodCleanup => bool.TryParse(Environment.GetEnvironmentVariable(VariableNames.DisableAutomaticPodCleanup), out var disableAutoCleanup) && disableAutoCleanup;
        public bool DisablePodEventsInTaskLog => bool.TryParse(Environment.GetEnvironmentVariable(VariableNames.DisablePodEventsInTaskLog), out var disable) && disable;
        public string PersistentVolumeSize => GetRequiredEnvVar(VariableNames.PersistentVolumeSize, "Unable to determine Persistent Volume Size");

        public bool IsMetricsEnabled => !bool.TryParse(Environment.GetEnvironmentVariable(VariableNames.IsMetricsEnabled), out var enableMetrics) || enableMetrics;
        public string? ScriptPodAffinityJson => Environment.GetEnvironmentVariable(VariableNames.ScriptPodAffinityJson);
        public string? ScriptPodTolerationsJson => Environment.GetEnvironmentVariable(VariableNames.ScriptPodTolerationsJson);
        public string? ScriptPodSecurityContextJson => Environment.GetEnvironmentVariable(VariableNames.ScriptPodSecurityContextJson);
        public string? ScriptPodProxiesSecretName => Environment.GetEnvironmentVariable(VariableNames.ScriptPodProxiesSecretName);

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}