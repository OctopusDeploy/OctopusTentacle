using System.Collections.Generic;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Configuration
{
    public interface IRegistryApplicationInstanceStore
    {
        ApplicationInstanceRecord? GetInstanceFromRegistry(ApplicationName name, string instanceName);

        IEnumerable<ApplicationInstanceRecord> GetListFromRegistry(ApplicationName name);
        void DeleteFromRegistry(ApplicationName name, string instanceName);
    }
}