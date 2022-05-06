using System;
using Newtonsoft.Json;
using Octopus.Configuration;
using Octopus.Shared.Configuration.Crypto;

namespace Octopus.Shared.Configuration
{
    public abstract class HierarchicalDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        readonly JsonSerializerSettings jsonSerializerSettings;

        protected HierarchicalDictionaryKeyValueStore(JsonSerializerSettings jsonSerializerSettings, bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
            this.jsonSerializerSettings = jsonSerializerSettings;
        }

        public override TData? Get<TData>(string name, TData? defaultValue, ProtectionLevel protectionLevel = ProtectionLevel.None) where TData : default
            => throw new NotImplementedException("This ");

        public override bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (value == null || value is string s && string.IsNullOrWhiteSpace(s))
            {
                Write(name, null);
                if (AutoSaveOnSet)
                    return Save();
                return true;
            }

            var valueAsObject = (object)value;

            if (protectionLevel == ProtectionLevel.MachineKey)
            {
                if (!(valueAsObject is string))
                    valueAsObject = JsonConvert.SerializeObject(value, jsonSerializerSettings);
                valueAsObject = MachineKeyEncryptor.Current.Encrypt((string)valueAsObject);
            }

            Write(name, valueAsObject);
            if (AutoSaveOnSet)
                return Save();

            return true;
        }
    }
}