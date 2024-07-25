namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesEnvironmentVariableNames
    {
        const string EnvVarPrefix = "OCTOPUS__K8STENTACLE";

        public static string GetPrefixedName(string suffix) => $"{EnvVarPrefix}__{suffix}".ToUpperInvariant();

        public static string Namespace => GetPrefixedName("NAMESPACE");
        public static string BootstrapRunnerExecutablePath => "BOOTSTRAPRUNNEREXECUTABLEPATH";

        public static string ScriptPodServiceAccountName => GetPrefixedName("PODSERVICEACCOUNTNAME");
        public static string ScriptPodImagePullSecretNames => GetPrefixedName("PODIMAGEPULLSECRETNAMES");
        public static string ScriptPodVolumeClaimName => GetPrefixedName("PODVOLUMECLAIMNAME");
        public static string ScriptPodResourceJson => GetPrefixedName("PODRESOURCEJSON");
        
        
        public static string NfsWatchdogImage => GetPrefixedName("NFSWATCHDOGIMAGE");
        
        

        public static string HelmReleaseName => GetPrefixedName("HELMRELEASENAME");
        public static string HelmChartVersion => GetPrefixedName("HELMCHARTVERSION");
        
        public const string ServerCommsAddresses = "ServerCommsAddresses";
        public const string ServerCommsAddress = "ServerCommsAddress";

        public static string PersistentVolumeSizeBytes => GetPrefixedName("PERSISTENTVOLUMETOTALBYTES");
        public static string PersistentVolumeFreeBytes => GetPrefixedName("PERSISTENTVOLUMEFREEBYTES");
        public static string PersistentVolumeSize => GetPrefixedName("PERSISTENTVOLUMESIZE");
        public static string EnableMetricsCapture => GetPrefixedName("ENABLEMETRICSCAPTURE");

        public static string ScriptPodMonitorTimeoutSeconds => GetPrefixedName("PODMONITORTIMEOUT");
        public static string ScriptPodsConsideredOrphanedAfter => GetPrefixedName("PODSCONSIDEREDORPHANEDAFTERMINUTES");
        public static string DisableAutomaticPodCleanup => GetPrefixedName("DISABLEAUTOPODCLEANUP");

    }
}