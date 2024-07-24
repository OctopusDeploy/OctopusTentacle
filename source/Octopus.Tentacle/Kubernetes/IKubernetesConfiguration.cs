using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesConfiguration
    {
        string Namespace { get;}
        string BootstrapRunnerExecutablePath { get;  }
        string ScriptPodServiceAccountName { get;  }
        IEnumerable<string?> ScriptPodImagePullSecretNames { get;  }
        string ScriptPodVolumeClaimName { get; }
        string? ScriptPodResourceJson { get;  }
        string ScriptPodResourceJsonVariableName { get;  }
        string? NfsWatchdogImage { get;  }
        string HelmReleaseName { get; }
        string HelmChartVersion { get; }    
        string[] ServerCommsAddresses { get; }
        string PodVolumeClaimName { get; }
        int? PodMonitorTimeoutSeconds { get;  }
        TimeSpan PodsConsideredOrphanedAfterTimeSpan { get; }
        bool DisableAutomaticPodCleanup { get;  }
        string PersistentVolumeSize { get;  }
        bool IsMetricsEnabled { get;  }
    }
}