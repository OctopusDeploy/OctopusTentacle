using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Octopus.Tentacle.Core.Configuration;

namespace Octopus.Tentacle.Core.Util
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


        public static TentacleEnvironmentVariable CreateTentacleHomeEnvironmentVariable(IHomeDirectoryProvider homeDirectoryProvider)
        {
            return new TentacleEnvironmentVariable(TentacleHome, homeDirectoryProvider.HomeDirectory);
        }

        /// <summary>
        /// Gets the file where deployment entries should be added.
        /// </summary>
        public static TentacleEnvironmentVariable JournalPathEnvVar(IHomeDirectoryProvider home, Func<string> alternateHomeDir)
        {
            var homeDir = home.HomeDirectory ?? alternateHomeDir();
            var journalPath = Path.Combine(homeDir, "DeploymentJournal.xml");
            return new TentacleEnvironmentVariable(TentacleJournal, journalPath);
        }

        /// <summary>
        /// Gets the file where package usages should be stored.
        /// </summary>
        public static TentacleEnvironmentVariable PackageRetentionJournalPathEnvVar(IHomeDirectoryProvider home, Func<string> alternateHomeDir)
        {
            var homeDir = home.HomeDirectory ?? alternateHomeDir();
            var packageRetentionJournalPath = Path.Combine(homeDir, "PackageRetentionJournal.json");
            return new TentacleEnvironmentVariable(CalamariPackageRetentionJournalPath, packageRetentionJournalPath);
        }

        /// <summary>
        /// Gets the environment variables for the Tentacle executable paths.
        /// </summary>
        public static IEnumerable<TentacleEnvironmentVariable> TentacleExecutablePathEnvVars(string exePath)
        {
            var programDirectory = Path.GetDirectoryName(exePath);
            return new[]
            {
                new TentacleEnvironmentVariable(TentacleExecutablePath, exePath),
                new TentacleEnvironmentVariable(TentacleProgramDirectoryPath, programDirectory),
                new TentacleEnvironmentVariable(AgentProgramDirectoryPath, programDirectory)
            };
        }

        /// <summary>
        /// Gets the environment variable for the .NET Framework description.
        /// </summary>
        public static TentacleEnvironmentVariable TentacleNetFrameworkDescriptionEnvVar()
        {
            return new TentacleEnvironmentVariable(TentacleNetFrameworkDescription, RuntimeInformation.FrameworkDescription);
        }

        /// <summary>
        /// Gets the environment variable for the Tentacle applications directory.
        /// </summary>
        public static TentacleEnvironmentVariable TentacleApplicationsEnvVar(string applicationDirectory)
        {
            return new TentacleEnvironmentVariable(TentacleApplications, applicationDirectory);
        }

        /// <summary>
        /// Gets the environment variable for the Tentacle instance name.
        /// </summary>
        public static TentacleEnvironmentVariable TentacleInstanceNameEnvVar(string? instanceName)
        {
            return new TentacleEnvironmentVariable(TentacleInstanceName, instanceName);
        }

        /// <summary>
        /// Gets the environment variable for the Tentacle version.
        /// </summary>
        public static TentacleEnvironmentVariable TentacleVersionEnvVar(string version)
        {
            return new TentacleEnvironmentVariable(TentacleVersion, version);
        }

        /// <summary>
        /// Gets most environment variables for scripts.
        /// This will be enough for running a Tentacle in testing.
        /// </summary>
        public static List<TentacleEnvironmentVariable> EnvironmentVariablesForScripts(
            IHomeDirectoryProvider home,
            Func<string> alternateHomeDir,
            string applicationDirectory,
            string? instanceName,
            string exePath,
            string version)
        {
            var envVars = new List<TentacleEnvironmentVariable>
            {
                CreateTentacleHomeEnvironmentVariable(home),
                TentacleApplicationsEnvVar(applicationDirectory),
                JournalPathEnvVar(home, alternateHomeDir),
                PackageRetentionJournalPathEnvVar(home, alternateHomeDir),
                TentacleInstanceNameEnvVar(instanceName),
                TentacleVersionEnvVar(version),
                TentacleNetFrameworkDescriptionEnvVar()
            };
            envVars.AddRange(TentacleExecutablePathEnvVars(exePath));
            return envVars;
        }
    }
}