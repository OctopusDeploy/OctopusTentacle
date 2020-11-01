using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationConfigurationWithMultipleInstances
    {
        IList<ApplicationInstanceRecord> ListInstances();
    }
}