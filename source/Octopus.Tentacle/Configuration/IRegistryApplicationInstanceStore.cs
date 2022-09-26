using System;
using System.Collections.Generic;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Configuration
{
    internal interface IRegistryApplicationInstanceStore
    {
        ApplicationInstanceRecord? GetInstanceFromRegistry(string instanceName);

        IEnumerable<ApplicationInstanceRecord> GetListFromRegistry();

        void DeleteFromRegistry(string instanceName);
    }
}