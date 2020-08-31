using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceStrategy
    {
        bool AnyInstancesConfigured();

        IList<ApplicationInstanceRecord> ListInstances();
        
        int Priority { get; }

        LoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance);
    }
    
    public interface IPersistedApplicationInstanceStrategy : IApplicationInstanceStrategy
    {
        PersistedApplicationInstanceRecord? GetInstance(string? instanceName);

        void MigrateInstance(PersistedApplicationInstanceRecord instanceRecord);

    }

    public interface IVirtualApplicationInstanceStrategy : IApplicationInstanceStrategy
    {
        ApplicationInstanceRecord? GetInstance();
    }
}