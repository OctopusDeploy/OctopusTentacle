using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IPersistedApplicationConfigurationStore : IApplicationConfigurationWithMultipleInstances
    {
        bool AnyInstancesConfigured();
        PersistedApplicationInstanceRecord? GetInstance(string? instanceName);

        void CreateDefaultInstance(string configurationFile, string? homeDirectory = null);
        void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null);

        void SaveInstance(PersistedApplicationInstanceRecord instanceRecord);
        void DeleteInstance(PersistedApplicationInstanceRecord instanceRecord);

        void MigrateInstance(PersistedApplicationInstanceRecord instanceRecord);
    }
}