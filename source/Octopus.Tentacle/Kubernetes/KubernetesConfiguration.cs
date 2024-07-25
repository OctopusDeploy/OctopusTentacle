using System;
using System.Collections.Generic;
using System.Linq;using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;
using Names = Octopus.Tentacle.Kubernetes.KubernetesEnvironmentVariableNames;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesConfiguration : IKubernetesConfiguration
    {
        public string Namespace => GetRequiredEnvVar(Names.Namespace, "Unable to determine Kubernetes namespace.");
        public string BootstrapRunnerExecutablePath => GetRequiredEnvVar(Names.BootstrapRunnerExecutablePath, "Unable to determine Bootstrap Runner Executable Path");
        public string ScriptPodServiceAccountName => GetRequiredEnvVar(Names.ScriptPodServiceAccountName, "Unable to determine Kubernetes Pod service account name.");
        
        public IEnumerable<string?> ScriptPodImagePullSecretNames => Environment.GetEnvironmentVariable(Names.ScriptPodImagePullSecretNames)
            ?.Split(',')
            .Select(str => str.Trim())
            .WhereNotNullOrWhiteSpace()
            .ToArray() ?? Array.Empty<string>();
        
        public string ScriptPodVolumeClaimName => GetRequiredEnvVar(Names.ScriptPodVolumeClaimName, "Unable to determine Kubernetes Pod persistent volume claim name.");
        public string? ScriptPodResourceJson => Environment.GetEnvironmentVariable(Names.ScriptPodResourceJson);
        
        public string? NfsWatchdogImage => Environment.GetEnvironmentVariable(Names.NfsWatchdogImage);

        
        public string HelmReleaseName => GetRequiredEnvVar(Names.HelmReleaseName, "Unable to determine Helm release name.");
        public string HelmChartVersion => GetRequiredEnvVar(Names.HelmChartVersion, "Unable to determine Helm chart version.");
        
        public string[] ServerCommsAddresses {
            get {
                var addresses = new List<string>();
                if (Environment.GetEnvironmentVariable(Names.ServerCommsAddress) is { Length: > 0 } addressString)
                {
                    addresses.Add(addressString);
                }
                if (Environment.GetEnvironmentVariable(Names.ServerCommsAddresses) is {} addressesString)
                {
                    addresses.AddRange(addressesString.Split(',').Where(a => !a.IsNullOrEmpty()));
                }
                return addresses.ToArray();
            }

        }

        public string PodVolumeClaimName => GetRequiredEnvVar(Names.ScriptPodVolumeClaimName, "Unable to determine Kubernetes Pod persistent volume claim name.");
        public int? PodMonitorTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable(Names.ScriptPodMonitorTimeoutSeconds), out var podMonitorTimeout) ? podMonitorTimeout : 10*60; //10min
        public TimeSpan PodsConsideredOrphanedAfterTimeSpan => TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable(Names.ScriptPodsConsideredOrphanedAfter), out var podsConsideredOrphanedAfterTimeSpan) ? podsConsideredOrphanedAfterTimeSpan : 10);
        public bool DisableAutomaticPodCleanup => bool.TryParse(Environment.GetEnvironmentVariable(Names.DisableAutomaticPodCleanup), out var disableAutoCleanup) && disableAutoCleanup;
       
        public string PersistentVolumeSize => GetRequiredEnvVar(Names.PersistentVolumeSize, "Unable to determine Persistent Volume Size");
        
        public bool IsMetricsEnabled => !bool.TryParse(Environment.GetEnvironmentVariable(Names.EnableMetricsCapture), out var enableMetrics) || enableMetrics;

        static string GetRequiredEnvVar(string variable, string errorMessage)
            => Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"{errorMessage} The environment variable '{variable}' must be defined with a non-null value.");
    }
}