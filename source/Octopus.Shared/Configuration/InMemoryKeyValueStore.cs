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
                return defaultValue;
            if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                return (TData) (object) bool.Parse((string) data);
            if (typeof(TData).IsEnum)
                return (TData) Enum.Parse(typeof(TData), ((string) data).Trim('"'));
            
            // See FlatDictionaryKeyValueStore.ValueNeedsToBeSerialized, some of the types are serialized, and will therefore expect to be
            // double quote delimited
            var dataType = typeof(TData);
            if (protectionLevel == ProtectionLevel.MachineKey || dataType.IsClass)
                return JsonConvert.DeserializeObject<TData>("\"" + data + "\"");
            
            return JsonConvert.DeserializeObject<TData>(data);
        }

        public bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return false;
        }

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return false;
        }

        public bool Remove(string name)
        {
            return false;
        }

        public bool Save()
        {
            return false;
        }
    }
}