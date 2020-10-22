using System;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceStore : IApplicationConfigurationWithMultipleInstances
    {
        bool AnyInstancesConfigured();
        ApplicationInstanceRecord? GetInstance(string instanceName);

        void CreateDefaultInstance(string configurationFile, string? homeDirectory = null);
        void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null);

        void SaveInstance(ApplicationInstanceRecord instanceRecord);
        void DeleteInstance(string instanceName);

        void MigrateInstance(ApplicationInstanceRecord instanceRecord);
    }
}