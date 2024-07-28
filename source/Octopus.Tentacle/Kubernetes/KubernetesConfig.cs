using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesConfig
    {
        const string ServerCommsAddressVariableName = "ServerCommsAddress";
        const string EnvVarPrefix = "OCTOPUS__K8STENTACLE";

        public static string NamespaceVariableName => $"{EnvVarPrefix}__NAMESPACE";
        public static string Namespace => GetRequiredEnvVar(NamespaceVariableName, "Unable to determine Kubernetes namespace.");
        public static string PodServiceAccountName => GetRequiredEnvVar($"{EnvVarPrefix}__PODSERVICEACCOUNTNAME", "Unable to determine Kubernetes Pod service account name.");
        public static string PodVolumeClaimName => GetRequiredEnvVar($"{EnvVarPrefix}__PODVOLUMECLAIMNAME", "Unable to determine Kubernetes Pod persistent volume claim name.");

        public static int PodMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__PODMONITORTIMEOUT"), out var podMonitorTimeout) ? podMonitorTimeout : 10*60; //10min
        public static string NfsWatchdogImageVariableName => $"{EnvVarPrefix}__NFSWATCHDOGIMAGE";
        public static string? NfsWatchdogImage => Environment.GetEnvironmentVariable(NfsWatchdogImageVariableName);

        public static TimeSpan PodsConsideredOrphanedAfterTimeSpan => TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__PODSCONSIDEREDORPHANEDAFTERMINUTES"), out var podsConsideredOrphanedAfterTimeSpan) ? podsConsideredOrphanedAfterTimeSpan : 10);
        public static bool DisableAutomaticPodCleanup => bool.TryParse(Environment.GetEnvironmentVariable($"{EnvVarPrefix}__DISABLEAUTOPODCLEANUP"), out var disableAutoCleanup) && disableAutoCleanup;

        public static string HelmReleaseNameVariableName => $"{EnvVarPrefix}__HELMRELEASENAME";
        public static string HelmReleaseName => GetRequiredEnvVar(HelmReleaseNameVariableName, "Unable to determine Helm release name.");

        public static string HelmChartVersionVariableName => $"{EnvVarPrefix}__HELMCHARTVERSION";
        public static string HelmChartVersion => GetRequiredEnvVar(HelmChartVersionVariableName, "Unable to determine Helm chart version.");

        public static string BootstrapRunnerExecutablePath => GetRequiredEnvVar("BOOTSTRAPRUNNEREXECUTABLEPATH", "Unable to determine Bootstrap Runner Executable Path");

        public static string PersistentVolumeSizeVariableName => $"{EnvVarPrefix}__PERSISTENTVOLUMESIZE";
        public static string PersistentVolumeSize => GetRequiredEnvVar(PersistentVolumeSizeVariableName, "Unable to determine Persistent Volume Size");

        public static string PersistentVolumeSizeBytesVariableName => $"{EnvVarPrefix}__PERSISTENTVOLUMETOTALBYTES";
        public static string PersistentVolumeFreeBytesVariableName => $"{EnvVarPrefix}__PERSISTENTVOLUMEFREEBYTES";

        public const string ServerCommsAddressesVariableName = "ServerCommsAddresses";

        public static string? ScriptPodContainerImage => Environment.GetEnvironmentVariable($"{EnvVarPrefix}__SCRIPTPODIMAGE");
        public static string ScriptPodContainerImageTag => Environment.GetEnvironmentVariable($"{EnvVarPrefix}__SCRIPTPODIMAGETAG") ?? "latest";
        public static string? ScriptPodPullPolicy => Environment.GetEnvironmentVariable($"{EnvVarPrefix}__SCRIPTPODPULLPOLICY");

        public static IEnumerable<string> PodImagePullSecretNames => Environment.GetEnvironmentVariable($"{EnvVarPrefix}__PODIMAGEPULLSECRETNAMES")
            ?.Split(',')
            .Select(str => str.Trim())
            .WhereNotNullOrWhiteSpace()
            .ToArray() ?? Array.Empty<string>();

        public static readonly string PodResourceJsonVariableName = $"{EnvVarPrefix}__PODRESOURCEJSON";
        public static string? PodResourceJson => Environment.GetEnvironmentVariable(PodResourceJsonVariableName);
        
        public static string MetricsEnableVariableName => $"{EnvVarPrefix}__ENABLEMETRICSCAPTURE";
        public static bool MetricsIsEnabled
        {
             get
             {
                 var envContent = Environment.GetEnvironmentVariable(MetricsEnableVariableName);
                if(bool.TryParse(envContent, out var result))
                {
                    return result;
                }
                return true;
            }
        }

        public static string[] ServerCommsAddresses {
            get {
                var addresses = new List<string>();
                if (Environment.GetEnvironmentVariable(ServerCommsAddressVariableName) is { Length: > 0 } addressString)
                {
                    addresses.Add(addressString);
                }
                if (Environment.GetEnvironmentVariable(ServerCommsAddressesVariableName) is {} addressesString)
                {
                    addresses.AddRange(addressesString.Split(',').Where(a => !a.IsNullOrEmpty()));
                }
                return addresses.ToArray();
            }

        }

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}
