using System;
using Newtonsoft.Json;
using Octopus.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;

namespace Octopus.Shared.Configuration
{
    public class InMemoryKeyValueStore : IKeyValueStore
    {
        readonly IMapEnvironmentVariablesToConfigItems mapper;

        public InMemoryKeyValueStore(IMapEnvironmentVariablesToConfigItems mapper)
        {
            this.mapper = mapper;
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return mapper.GetConfigurationValue(name);
        }

        public TData Get<TData>(string name, TData defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var data = mapper.GetConfigurationValue(name);

            if (data == null)
                return default!;
            if (typeof(TData) == typeof(string))
                return (TData)(object) data;
            if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                return (TData) (object) bool.Parse((string) data);
            if (typeof(TData).IsEnum)
                return (TData) Enum.Parse(typeof(TData), ((string) data).Trim('"'));

            return JsonConvert.DeserializeObject<TData>(data);
        }

        public void Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value through the EnvFileBaseKeyValueStore is not supported");
        }

        public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value through the EnvFileBaseKeyValueStore is not supported");
        }

        public void Remove(string name)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value through the EnvFileBaseKeyValueStore is not supported");
        }

        public void Save()
        {
            throw new InvalidOperationException("Persisting configuration values through the EnvFileBaseKeyValueStore is not supported");
        }
    }
}