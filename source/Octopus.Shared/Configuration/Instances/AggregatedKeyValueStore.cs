using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    class AggregatedKeyValueStore : IKeyValueStore
    {
        readonly IAggregatableKeyValueStore[] configurations;

        public AggregatedKeyValueStore(IAggregatableKeyValueStore[] configurations)
        {
            this.configurations = configurations;
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
            => Get(name, default(string?), protectionLevel);

        [return: NotNullIfNotNull("defaultValue")]
        public TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            // the default value must not be sent to the aggregated config, it gets applied at this level if
            // none of the configurations return a value
            var result = configurations
                .Select(c => c.TryGet<TData>(name, protectionLevel))
                .FirstOrDefault(x => x.foundResult);

            return result.foundResult ? result.value : defaultValue;
        }
    }
}