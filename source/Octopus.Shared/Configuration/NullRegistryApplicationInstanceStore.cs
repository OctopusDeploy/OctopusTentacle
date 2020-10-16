using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Configuration
{
    class NullRegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        public ApplicationInstanceRecord? GetInstanceFromRegistry(string instanceName)
        {
            return null;
        }

        public IEnumerable<ApplicationInstanceRecord> GetListFromRegistry()
        {
            return Enumerable.Empty<ApplicationInstanceRecord>();
        }

        public void DeleteFromRegistry(string instanceName)
        {
        }
    }
}