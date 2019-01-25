using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Configuration
{
    public class NullRegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        public ApplicationInstanceRecord GetInstanceFromRegistry(ApplicationName name, string instanceName)
        {
            return null;
        }

        public IEnumerable<ApplicationInstanceRecord> GetListFromRegistry(ApplicationName name)
        {
            return Enumerable.Empty<ApplicationInstanceRecord>();
        }

        public void DeleteFromRegistry(ApplicationName name, string instanceName)
        {
            return;
        }
    }
}