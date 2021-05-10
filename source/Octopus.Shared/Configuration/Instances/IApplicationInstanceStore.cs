using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceStore
    {
        ApplicationInstanceRecord LoadInstanceDetails(string? instanceName);
        void RegisterInstance(ApplicationInstanceRecord instanceRecord);
        void DeleteInstance(string instanceName);
        IList<ApplicationInstanceRecord> ListInstances();
        
    }
}