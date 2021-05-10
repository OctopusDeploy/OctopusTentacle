using System;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationConfigurationStrategy
    {
        int Priority { get; }
        IAggregatableKeyValueStore? LoadContributedConfiguration();
    }
}