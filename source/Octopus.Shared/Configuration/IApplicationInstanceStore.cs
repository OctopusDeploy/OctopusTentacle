using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceStore
    {
        bool AnyInstancesConfigured(ApplicationName name);
        ApplicationInstanceRecord? GetInstance(ApplicationName name, string instanceName);
        IList<ApplicationInstanceRecord> ListInstances(ApplicationName name);
        void SaveInstance(ApplicationInstanceRecord instanceRecord);
        void DeleteInstance(ApplicationInstanceRecord instanceRecord);

        void MigrateInstance(ApplicationInstanceRecord instanceRecord);
    }
}