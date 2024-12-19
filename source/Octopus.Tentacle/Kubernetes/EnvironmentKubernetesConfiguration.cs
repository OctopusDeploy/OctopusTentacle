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
            public const string EnvVarPrefix = "OCTOPUS__K8STENTACLE";

            public const string PersistentVolumeSizeBytesVariableName = $"{EnvVarPrefix}__PERSISTENTVOLUMETOTALBYTES";
            public const string PersistentVolumeFreeBytesVariableName = $"{EnvVarPrefix}__PERSISTENTVOLUMEFREEBYTES";
            public const string NamespaceVariableName = $"{EnvVarPrefix}__NAMESPACE";

            public const string HelmReleaseNameVariableName = $"{EnvVarPrefix}__HELMRELEASENAME";
            public const string HelmChartVersionVariableName = $"{EnvVarPrefix}__HELMCHARTVERSION";
            public const string ServerCommsAddressesVariableName = "ServerCommsAddresses";

            public const string PodAffinityJsonVariableName = $"{EnvVarPrefix}__PODAFFINITYJSON";
            public const string PodTolerationsJsonVariableName = $"{EnvVarPrefix}__PODTOLERATIONSJSON";
            public const string PodSecurityContextJsonVariableName = $"{EnvVarPrefix}__PODSECURITYCONTEXTJSON";
            public const string PodResourceJsonVariableName = $"{EnvVarPrefix}__PODRESOURCEJSON";
        }

        public string Namespace => GetRequiredEnvVar(VariableNames.NamespaceVariableName, "Unable to determine Kubernetes namespace.");
        public string BootstrapRunnerExecutablePath => GetRequiredEnvVar("BOOTSTRAPRUNNEREXECUTABLEPATH", "Unable to determine Bootstrap Runner Executable Path");
        public string ScriptPodServiceAccountName => GetRequiredEnvVar($"{VariableNames.EnvVarPrefix}__SCRIPTPODSERVICEACCOUNTNAME", "Unable to determine Kubernetes Pod service account name.");

        public IEnumerable<string?> ScriptPodImagePullSecretNames => Environment.GetEnvironmentVariable($"{VariableNames.EnvVarPrefix}__PODIMAGEPULLSECRETNAMES")
            ?.Split(',')
            .Select(str => str.Trim())
            .WhereNotNullOrWhiteSpace()
            .ToArray() ?? [];

        public string ScriptPodVolumeClaimName => GetRequiredEnvVar($"{VariableNames.EnvVarPrefix}__PODVOLUMECLAIMNAME", "Unable to determine Kubernetes Pod persistent volume claim name.");
        public string? ScriptPodResourceJson => Environment.GetEnvironmentVariable(ScriptPodResourceJsonVariableName);
        public string ScriptPodResourceJsonVariableName => $"{VariableNames.EnvVarPrefix}__PODRESOURCEJSON";

        public static string NfsWatchdogImageVariableName => $"{VariableNames.EnvVarPrefix}__NFSWATCHDOGIMAGE";
        public string? NfsWatchdogImage => Environment.GetEnvironmentVariable(NfsWatchdogImageVariableName);

        public static string HelmReleaseNameVariableName => $"{VariableNames.EnvVarPrefix}__HELMRELEASENAME";
        public static string HelmChartVersionVariableName => $"{VariableNames.EnvVarPrefix}__HELMCHARTVERSION";
        const string ServerCommsAddressVariableName = "ServerCommsAddress";
        public const string ServerCommsAddressesVariableName = "ServerCommsAddresses";

        public string HelmReleaseName => GetRequiredEnvVar(HelmReleaseNameVariableName, "Unable to determine Helm release name.");
        public string HelmChartVersion => GetRequiredEnvVar(HelmChartVersionVariableName, "Unable to determine Helm chart version.");

        public string[] ServerCommsAddresses
        {
            get
            {
                var addresses = new List<string>();
                if (Environment.GetEnvironmentVariable(ServerCommsAddressVariableName) is { Length: > 0 } addressString)
                {
                    addresses.Add(addressString);
                }

                if (Environment.GetEnvironmentVariable(ServerCommsAddressesVariableName) is { } addressesString)
                {
                    addresses.AddRange(addressesString.Split(',').Where(a => !a.IsNullOrEmpty()));
                }

                return addresses.ToArray();
            }
        }

        public string PodVolumeClaimName => GetRequiredEnvVar($"{VariableNames.EnvVarPrefix}__PODVOLUMECLAIMNAME", "Unable to determine Kubernetes Pod persistent volume claim name.");
        public int? PodMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable($"{VariableNames.EnvVarPrefix}__PODMONITORTIMEOUT"), out var podMonitorTimeout) ? podMonitorTimeout : 10 * 60; //10min
        public TimeSpan PodsConsideredOrphanedAfterTimeSpan => TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable($"{VariableNames.EnvVarPrefix}__PODSCONSIDEREDORPHANEDAFTERMINUTES"), out var podsConsideredOrphanedAfterTimeSpan) ? podsConsideredOrphanedAfterTimeSpan : 10);
        public bool DisableAutomaticPodCleanup => bool.TryParse(Environment.GetEnvironmentVariable($"{VariableNames.EnvVarPrefix}__DISABLEAUTOPODCLEANUP"), out var disableAutoCleanup) && disableAutoCleanup;

        public static string PersistentVolumeSizeVariableName => $"{VariableNames.EnvVarPrefix}__PERSISTENTVOLUMESIZE";
        public string PersistentVolumeSize => GetRequiredEnvVar(PersistentVolumeSizeVariableName, "Unable to determine Persistent Volume Size");

        public bool IsMetricsEnabled => !bool.TryParse(Environment.GetEnvironmentVariable($"{VariableNames.EnvVarPrefix}__ENABLEMETRICSCAPTURE"), out var enableMetrics) || enableMetrics;

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}