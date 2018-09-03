using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration
{
    public interface IRegistryApplicationInstanceStore
    {
        List<ApplicationInstanceRecord> GetListFromRegistry(ApplicationName name);
        void DeleteFromRegistry(ApplicationName name, string instanceName);
    }
}