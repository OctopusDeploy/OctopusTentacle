using System;
using Octopus.Tentacle.Maintenance;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TentacleBuilderExtensionMethods
    {
        public static ITentacleBuilder WithWorkspaceCleaningSettings(this ITentacleBuilder builder, TimeSpan cleanerDelay, TimeSpan deleteWorkspacesOlderThan)
        {
            return builder
                .WithEnvironmentVariable(WorkspaceCleanerConfiguration.CleanerDelayEnvironmentVariableName, cleanerDelay)
                .WithEnvironmentVariable(WorkspaceCleanerConfiguration.DeleteWorkspacesOlderThanTimeSpanEnvironmentVariableName, deleteWorkspacesOlderThan);
        }
    }
}