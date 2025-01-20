using System;

namespace Octopus.Tentacle.Variables
{
    public static class EnvironmentVariables
    {
        public const string TentacleProxyUsername = "TentacleProxyUsername";
        public const string TentacleProxyPassword = "TentacleProxyPassword";
        public const string TentacleProxyHost = "TentacleProxyHost";
        public const string TentacleProxyPort = "TentacleProxyPort";
        public const string TentacleUseDefaultProxy = "TentacleUseDefaultProxy";
        public const string TentacleVersion = "TentacleVersion";
        public const string TentacleNetFrameworkDescription  = "TentacleNetFrameworkDescription";
        public const string TentacleCertificateSignatureAlgorithm = "TentacleCertificateSignatureAlgorithm";
        public const string TentacleHome = "TentacleHome";
        public const string TentacleApplications = "TentacleApplications";
        public const string TentacleJournal = "TentacleJournal";
        public const string CalamariPackageRetentionJournalPath = "CalamariPackageRetentionJournalPath";
        public const string TentacleInstanceName = "TentacleInstanceName";
        public const string TentacleExecutablePath = "TentacleExecutablePath";
        public const string TentacleProgramDirectoryPath = "TentacleProgramDirectoryPath";
        public const string AgentProgramDirectoryPath = "AgentProgramDirectoryPath";
        public const string TentacleTcpKeepAliveEnabled = "TentacleTcpKeepAliveEnabled";
        public const string TentacleUseRecommendedTimeoutsAndLimits = "TentacleUseRecommendedTimeoutsAndLimits";
        public const string TentacleMachineConfigurationHomeDirectory = "TentacleMachineConfigurationHomeDirectory";
        public const string TentaclePollingConnectionCount = "TentaclePollingConnectionCount";
        public const string NfsWatchdogDirectory = "watchdog_directory";
        public static string TentacleUseTcpNoDelay = "TentacleUseTcpNoDelay";
        public static string TentacleUseAsyncListener = "TentacleUseAsyncListener";
        public static string DefaultLogDirectory = "DefaultLogDirectory";
    }
}