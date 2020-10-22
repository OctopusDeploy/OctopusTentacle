using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceLocator
    {
        bool AnyInstancesConfigured();

        ApplicationInstanceRecord? GetInstance(string instanceName);

        IList<ApplicationInstanceRecord> ListInstances();
    }
}