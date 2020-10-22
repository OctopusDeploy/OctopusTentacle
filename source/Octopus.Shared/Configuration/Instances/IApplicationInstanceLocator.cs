using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceLocator
    {
        bool AnyInstancesConfigured();

        PersistedApplicationInstanceRecord? GetInstance(string instanceName);

        IList<PersistedApplicationInstanceRecord> ListInstances();
    }
}