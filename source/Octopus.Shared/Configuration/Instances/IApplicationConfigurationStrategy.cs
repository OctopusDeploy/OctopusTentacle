using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationConfigurationStrategy
    {
        int Priority { get; }

        IKeyValueStore? LoadedConfiguration(ApplicationRecord applicationInstance);
    }
}