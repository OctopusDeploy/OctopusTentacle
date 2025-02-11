using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesConfiguration
    {
        string Namespace { get; }
        string BootstrapRunnerExecutablePath { get; }
        string ScriptPodServiceAccountName { get; }
        IEnumerable<string?> ScriptPodImagePullSecretNames { get; }
        string ScriptPodVolumeClaimName { get; }
        string? ScriptPodResourceJson { get; }
        string? ScriptPodAffinityJson { get; }
        string? ScriptPodTolerationsJson { get; }
        string? ScriptPodSecurityContextJson { get; }
        string? ScriptPodProxiesSecretName { get; }
        string? ScriptPodContainerImage { get; }
        string ScriptPodContainerImageTag { get; }
        string? ScriptPodPullPolicy { get; }
        string? NfsWatchdogImage { get; }
        string HelmReleaseName { get; }
        string HelmChartVersion { get; }
        string[] ServerCommsAddresses { get; }
        int? ScriptPodMonitorTimeoutSeconds { get; }
        TimeSpan ScriptPodConsideredOrphanedAfterTimeSpan { get; }
        bool DisableAutomaticPodCleanup { get; }
        bool DisablePodEventsInTaskLog { get; }
        string PersistentVolumeSize { get; }
        bool IsMetricsEnabled { get; }
    }
}