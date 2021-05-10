using System;

namespace Octopus.Shared.Configuration.Instances
{
    /// <summary>
    /// Allows additional contribution of configuration to extend the configuration provided by the "primary" configuration
    /// </summary>
    public interface IApplicationConfigurationStrategy
    {
        int Priority { get; }
        IAggregatableKeyValueStore? LoadContributedConfiguration();
    }
}