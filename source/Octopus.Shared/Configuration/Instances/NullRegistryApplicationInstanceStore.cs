using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Configuration.Instances
{
    public class NullRegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        public ApplicationInstanceRecord? GetInstanceFromRegistry(string instanceName)
            => null;

        public IEnumerable<ApplicationInstanceRecord> GetListFromRegistry()
            => Enumerable.Empty<ApplicationInstanceRecord>();

        public void DeleteFromRegistry(string instanceName)
        {
        }
    }
}