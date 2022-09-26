using System;
using Newtonsoft.Json;
using Octopus.Configuration;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Configuration
{
    public class InMemoryKeyValueStore : IAggregatableKeyValueStore
    {
        private readonly IMapEnvironmentValuesToConfigItems mapper;

        public InMemoryKeyValueStore(IMapEnvironmentValuesToConfigItems mapper)
        {
            this.mapper = mapper;
        }

        public (bool foundResult, TData? value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            object? data = mapper.GetConfigurationValue(name);

            if (data == null)
                return (false, default!);
            if (typeof(TData) == typeof(string))
                return (true, (TData)data);
            if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                return (true, (TData)(object)bool.Parse((string)data));
            if (typeof(TData).IsEnum)
                return (true, (TData)Enum.Parse(typeof(TData), ((string)data).Trim('"')));

            // See FlatDictionaryKeyValueStore.ValueNeedsToBeSerialized, some of the types are serialized, and will therefore expect to be
            // double quote delimited
            var dataType = typeof(TData);
            if (protectionLevel == ProtectionLevel.MachineKey || dataType == typeof(byte[]))
                return (true, JsonConvert.DeserializeObject<TData>("\"" + data + "\""));

            return (true, JsonConvert.DeserializeObject<TData>((string)data));
        }
    }
}