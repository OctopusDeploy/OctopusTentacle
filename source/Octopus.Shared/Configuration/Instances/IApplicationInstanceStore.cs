using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    internal interface IApplicationInstanceStore
    {
        bool AnyInstancesConfigured();

        ApplicationInstanceRecord? GetInstance(string instanceName);

        IList<ApplicationInstanceRecord> ListInstances();

        void SaveInstance(ApplicationInstanceRecord instanceRecord);

        void DeleteInstance(string instanceName);

        void MigrateInstance(ApplicationInstanceRecord instanceRecord);
    }
}