using System;

namespace Octopus.Shared.Variables
{
    public static class EnvironmentVariables
    {
        public const string TentacleProxyUsername = "TentacleProxyUsername";
        public const string TentacleProxyPassword = "TentacleProxyPassword";
        public const string TentacleVersion = "TentacleVersion";
        public const string TentacleHome = "TentacleHome";
        public const string TentacleApplications = "TentacleApplications";
        public const string TentacleJournal = "TentacleJournal";
        public const string TentacleInstanceName = "TentacleInstanceName";
        public const string TentacleExecutablePath = "TentacleExecutablePath";
        public const int DefaultMinutesToDeleteOfflineTransientMachines = 60;
    }
}