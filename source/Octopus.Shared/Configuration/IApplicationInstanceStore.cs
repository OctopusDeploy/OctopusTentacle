using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceStore
    {
        IList<ApplicationInstanceRecord> ListInstances(ApplicationName name);
        ApplicationInstanceRecord GetInstance(ApplicationName name, string instanceName);
        ApplicationInstanceRecord GetDefaultInstance(ApplicationName name);
        void SaveInstance(ApplicationInstanceRecord instanceRecord);
        void DeleteInstance(ApplicationInstanceRecord instanceRecord);
    }
}