using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Configuration.Instances
{
    public class NullRegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        public PersistedApplicationInstanceRecord? GetInstanceFromRegistry(string instanceName)
        {
            return null;
        }

        public IEnumerable<PersistedApplicationInstanceRecord> GetListFromRegistry()
        {
            return Enumerable.Empty<PersistedApplicationInstanceRecord>();
        }

        public void DeleteFromRegistry(string instanceName)
        {
        }
    }
}