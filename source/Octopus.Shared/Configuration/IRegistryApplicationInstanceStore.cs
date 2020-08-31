using System.Collections.Generic;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Configuration
{
    public interface IRegistryApplicationInstanceStore
    {
        PersistedApplicationInstanceRecord? GetInstanceFromRegistry(string instanceName);

        IEnumerable<PersistedApplicationInstanceRecord> GetListFromRegistry();

        void DeleteFromRegistry(string instanceName);
    }
}