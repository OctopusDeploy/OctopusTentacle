using System;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceRegistry : IApplicationConfigurationWithMultipleInstances
    {
        ApplicationInstanceRecord? GetInstance(string instanceName);
        void RegisterInstance(ApplicationInstanceRecord instanceRecord);
        void DeleteInstance(string instanceName);

        void MigrateInstance(ApplicationInstanceRecord instanceRecord);
    }
}