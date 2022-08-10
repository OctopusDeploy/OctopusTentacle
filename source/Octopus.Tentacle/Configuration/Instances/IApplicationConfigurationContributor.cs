using System;

namespace Octopus.Tentacle.Configuration.Instances
{
    /// <summary>
    /// Allows additional contribution of configuration to extend the configuration provided by the "primary" configuration
    /// </summary>
    public interface IApplicationConfigurationContributor
    {
        int Priority { get; }
        IAggregatableKeyValueStore? LoadContributedConfiguration();
    }
}