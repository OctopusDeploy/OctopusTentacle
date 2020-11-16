using System;
using System.Collections.Generic;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Configuration
{
    interface IRegistryApplicationInstanceStore
    {
        ApplicationInstanceRecord? GetInstanceFromRegistry(string instanceName);

        IEnumerable<ApplicationInstanceRecord> GetListFromRegistry();

        void DeleteFromRegistry(string instanceName);
    }
}