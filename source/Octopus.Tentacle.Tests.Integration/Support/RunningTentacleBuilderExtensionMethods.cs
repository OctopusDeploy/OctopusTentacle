using System;
using Octopus.Tentacle.Maintenance;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class RunningTentacleBuilderExtensionMethods
    {
        public static IRunningTentacleBuilder WithWorkspaceCleaningSettings(this IRunningTentacleBuilder builder, TimeSpan cleanerDelay, TimeSpan deleteWorkspacesOlderThan)
        {
            return builder
                .WithEnvironmentVariable(WorkspaceCleanerConfiguration.CleanerDelayEnvironmentVariableName, cleanerDelay)
                .WithEnvironmentVariable(WorkspaceCleanerConfiguration.DeleteWorkspacesOlderThanTimeSpanEnvironmentVariableName, deleteWorkspacesOlderThan);
        }
    }
}