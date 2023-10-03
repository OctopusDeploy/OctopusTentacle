using System;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;

namespace Octopus.Tentacle.Maintenance
{
    public class WorkspaceCleanerConfiguration
    {
        public const string DeleteWorkspacesOlderThanTimeSpanEnvironmentVariableName = "TENTACLE_WORKSPACE_CLEANER_DELETE_OLDER_THAN";
        public const string CleanerDelayEnvironmentVariableName = "TENTACLE_WORKSPACE_CLEANER_DELAY";

        public TimeSpan CleaningDelay { get; }
        public TimeSpan DeleteWorkspacesOlderThanTimeSpan { get; }

        public WorkspaceCleanerConfiguration(IEnvironmentVariableReader environmentVariableReader)
        {
            CleaningDelay = environmentVariableReader.GetOrDefault(CleanerDelayEnvironmentVariableName, TimeSpan.FromHours(2));
            DeleteWorkspacesOlderThanTimeSpan = environmentVariableReader.GetOrDefault(DeleteWorkspacesOlderThanTimeSpanEnvironmentVariableName, TimeSpan.FromDays(1));
        }
    }
}