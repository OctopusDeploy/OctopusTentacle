using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceRepository
    {
        IList<ApplicationInstance> ListInstances(ApplicationName name);
        ApplicationInstance GetInstance(ApplicationName name, string instanceName);
        ApplicationInstance GetDefaultInstance(ApplicationName name);
        void SaveInstance(ApplicationInstance instance);
    }
}