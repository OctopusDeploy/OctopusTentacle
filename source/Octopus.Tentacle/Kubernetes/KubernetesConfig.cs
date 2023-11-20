﻿using System;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesConfig
    {
        public static string Namespace => Environment.GetEnvironmentVariable("OCTOPUS__TENTACLE__K8SNAMESPACE")
            ?? throw new InvalidOperationException("Unable to determine Kubernetes namespace. An environment variable 'OCTOPUS__TENTACLE__K8SNAMESPACE' must be defined.");

        public static bool UseJobs => bool.TryParse(Environment.GetEnvironmentVariable("OCTOPUS__TENTACLE__K8SUSEJOBS"), out var useJobs) && useJobs;

        public static string ServiceAccountName => Environment.GetEnvironmentVariable("OCTOPUS__TENTACLE__K8SSERVICEACCOUNTNAME")
            ?? throw new InvalidOperationException("Unable to determine Kubernetes Job service account name. An environment variable 'OCTOPUS__TENTACLE__K8SSERVICEACCOUNTNAME' must be defined.");

        public static string JobVolumeYaml => Environment.GetEnvironmentVariable("OCTOPUS__TENTACLE__K8SJOBVOLUMEYAML")
            ?? throw new InvalidOperationException("Unable to determine Kubernetes Job volume yaml. An environment variable 'OCTOPUS__TENTACLE__K8SJOBVOLUMEYAML' must be defined.");
    }
}