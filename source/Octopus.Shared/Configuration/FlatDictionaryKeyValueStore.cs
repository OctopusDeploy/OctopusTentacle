using System;
using Newtonsoft.Json;
using Octopus.Configuration;
using Octopus.Shared.Configuration.Crypto;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Configuration
{
    public abstract class FlatDictionaryKeyValueStore : DictionaryKeyValueStore, IAggregatableKeyValueStore
    {
        protected readonly JsonSerializerSettings JsonSerializerSettings;

        protected FlatDictionaryKeyValueStore(JsonSerializerSettings jsonSerializerSettings, bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public override TData Get<TData>(string name, TData defaultValue, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            string? valueAsString = null;
            try
            {
                var data = Read(name);
                if (data == null)
                    return defaultValue;
                valueAsString = data as string;
                if (valueAsString == null || string.IsNullOrWhiteSpace(valueAsString))
                    return defaultValue;

                if (protectionLevel == ProtectionLevel.MachineKey)
                    data = MachineKeyEncryptor.Current.Decrypt(valueAsString);

                if (typeof(TData) == typeof(string))
                    return (TData)data;
                if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                    return (TData)(object)bool.Parse((string)data);
                if (typeof(TData).IsEnum)
                    return (TData)Enum.Parse(typeof(TData), ((string)data).Trim('"'));

                return JsonConvert.DeserializeObject<TData>((string)data, JsonSerializerSettings);
            }
            catch (Exception e)
            {
                if (protectionLevel == ProtectionLevel.None)
                    throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'. Value was '{valueAsString}'.", e);
                throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'.", e);
            }
        }

        public (bool foundResult, TData value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            string? valueAsString = null;
            try
            {
                var data = Read(name);
                if (data == null)
                    return (false, default!);
                valueAsString = data as string;
                if (valueAsString == null || string.IsNullOrWhiteSpace(valueAsString))
                    return (false, default!);

                if (protectionLevel == ProtectionLevel.MachineKey)
                    data = MachineKeyEncryptor.Current.Decrypt(valueAsString);

                if (typeof(TData) == typeof(string))
                    return (true, (TData)data);
                if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                    return (true, (TData)(object)bool.Parse((string)data));
                if (typeof(TData).IsEnum)
                    return (true, (TData)Enum.Parse(typeof(TData), ((string)data).Trim('"')));

                return (true, JsonConvert.DeserializeObject<TData>((string)data, JsonSerializerSettings));
            }
            catch (Exception e)
            {
                if (protectionLevel == ProtectionLevel.None)
                    throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'. Value was '{valueAsString}'.", e);
                throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'.", e);
            }
        }

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

            if (ValueNeedsToBeSerialized(protectionLevel, valueAsObject))
                valueAsObject = JsonConvert.SerializeObject(value, JsonSerializerSettings);

            if (protectionLevel == ProtectionLevel.MachineKey && valueAsObject != null)
                valueAsObject = MachineKeyEncryptor.Current.Encrypt((string)valueAsObject);

            Write(name, valueAsObject);
            if (AutoSaveOnSet)
                return Save();

            return true;
        }

        protected virtual bool ValueNeedsToBeSerialized(ProtectionLevel protectionLevel, object valueAsObject)
        {
            //null would end up as "null" rather than empty
            if (valueAsObject == null)
                return false;

            //bool/int/string etc will work fine directly when used as ToString()
            //custom types will end up as the object type instead of anything useful
            if (valueAsObject.GetType().ToString() == valueAsObject.ToString())
                return true;

            //dont stick extra quotes around a string
            if (valueAsObject is string)
                return false;

            //need to convert bool/int/etc to a string for it to be encrypted
            if (protectionLevel == ProtectionLevel.MachineKey)
                return true;

            return false;
        }
    }
}