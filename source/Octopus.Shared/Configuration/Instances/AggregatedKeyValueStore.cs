using System;
using System.Linq;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    class AggregatedKeyValueStore : IKeyValueStore
    {
        readonly IKeyValueStore[] configurations;

        public AggregatedKeyValueStore(IKeyValueStore[] configurations)
        {
            this.configurations = configurations;
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return configurations.Select(c => c.Get(name, protectionLevel)).FirstOrDefault(x => x != null);
        }

        public TData Get<TData>(string name, TData defaultValue = default(TData), ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return configurations
                .Select(c => c.Get<TData>(name, defaultValue, protectionLevel))
                .FirstOrDefault(x => x != null);
        }
    }
}