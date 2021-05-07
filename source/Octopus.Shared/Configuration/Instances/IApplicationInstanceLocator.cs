using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceLocator
    {
        ApplicationInstanceRecord? GetInstance(string instanceName);

        IList<ApplicationInstanceRecord> ListInstances();
    }
}