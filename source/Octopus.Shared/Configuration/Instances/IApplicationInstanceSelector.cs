using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceSelector
    {
        ApplicationName ApplicationName { get; }
        
        ILoadedApplicationInstance GetCurrentInstance();

        IList<ApplicationInstanceRecord> ListInstances();
        
        bool TryGetCurrentInstance([NotNullWhen(true)] out ILoadedApplicationInstance? instance);

        /// <summary>
        /// Gets a key value store that can be modified, if an appropriate IApplicationInstanceStrategy is in play
        /// </summary>
        IModifiableKeyValueStore? ModifiableKeyValueStore { get; }
        
        /// <summary>
        /// Gets a HomeConfiguration that can be modified, if an appropriate IApplicationInstanceStrategy is in play
        /// </summary>
        IModifiableHomeConfiguration? ModifiableHomeConfiguration { get; }

        /// <summary>
        /// Gets a ProxyConfiguration that can be modified, if an appropriate IApplicationInstanceStrategy is in play
        /// </summary>
        IModifiableProxyConfiguration? ModifiableProxyConfiguration { get; }
    }
}