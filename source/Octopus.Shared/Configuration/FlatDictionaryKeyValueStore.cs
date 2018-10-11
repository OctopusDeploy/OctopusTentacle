using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public abstract class FlatDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        protected FlatDictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        public override TData Get<TData>(string name, TData defaultValue, ProtectionLevel protectionLevel  = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var data = Read(name);
            var valueAsString = data as string;
            if (string.IsNullOrWhiteSpace(valueAsString))
                return defaultValue;

            if (protectionLevel == ProtectionLevel.MachineKey)
            {
                data = MachineKeyEncrypter.Current.Decrypt(valueAsString);
            }

            if (typeof(TData) == typeof(string))
                return (TData)data;

            return JsonConvert.DeserializeObject<TData>((string)data);
        }

        private void SetInternal(string name, string value, ProtectionLevel protectionLevel  = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (string.IsNullOrWhiteSpace(value))
            {
                Write(name, null);
                if (AutoSaveOnSet)
                    Save();
                return;
            }

            if (protectionLevel == ProtectionLevel.MachineKey)
            {
                value = MachineKeyEncrypter.Current.Encrypt(value);
            }

            Write(name, value);
            if (AutoSaveOnSet)
                Save();
        }

        public override void Set<TData>(string name, TData value, ProtectionLevel protectionLevel  = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (typeof(TData) == typeof(string))
                SetInternal(name, (string)(object)value, protectionLevel);
            else
                SetInternal(name, JsonConvert.SerializeObject(value), protectionLevel);
        }
    }
}
