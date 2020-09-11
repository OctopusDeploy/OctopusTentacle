using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceSelector
    {
        ApplicationName ApplicationName { get; }
        
        ILoadedApplicationInstance GetCurrentInstance();

        IList<ApplicationInstanceRecord> ListInstances();
        
        bool TryGetCurrentInstance([NotNullWhen(true)] out ILoadedApplicationInstance? instance);
    }
}