using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceStore
    {
        bool AnyInstancesConfigured();
        ApplicationInstanceRecord? GetInstance(string instanceName);
        IList<ApplicationInstanceRecord> ListInstances();
        void SaveInstance(ApplicationInstanceRecord instanceRecord);
        void DeleteInstance(ApplicationInstanceRecord instanceRecord);

        void MigrateInstance(ApplicationInstanceRecord instanceRecord);
    }
}