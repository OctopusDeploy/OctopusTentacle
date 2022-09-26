using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Configuration.Instances
{
    public class NullRegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
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