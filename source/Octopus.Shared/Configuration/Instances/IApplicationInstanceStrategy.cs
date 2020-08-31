using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceStrategy
    {
        bool AnyInstancesConfigured();

        IList<ApplicationInstanceRecord> ListInstances();
        
        int Priority { get; }

        LoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance);
    }
}