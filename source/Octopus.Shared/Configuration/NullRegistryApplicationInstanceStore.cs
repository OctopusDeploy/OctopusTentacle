using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Configuration
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