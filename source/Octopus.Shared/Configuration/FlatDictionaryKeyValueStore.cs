using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Octopus.Shared.Configuration
{
    public abstract class FlatDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        protected FlatDictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        public override TData Get<TData>(string name, TData defaultValue, DataProtectionScope? protectionScope)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var s = Read(name);
            var valueAsString = s as string;
            if (string.IsNullOrWhiteSpace(valueAsString))
                return defaultValue;

            if (protectionScope != null)
            {
                var decryptedBytes = ProtectedData.Unprotect(Convert.FromBase64String(valueAsString), null, protectionScope.Value);
                s = Encoding.UTF8.GetString(decryptedBytes);
            }

            if (typeof(TData) == typeof(string))
                return (TData)s;

            return JsonConvert.DeserializeObject<TData>((string)s);
        }

        private void SetInternal(string name, string value, DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (string.IsNullOrWhiteSpace(value))
            {
                Write(name, null);
                if (AutoSaveOnSet)
                    Save();
                return;
            }

            var v = value;

            if (protectionScope != null)
            {
                v = Convert.ToBase64String(
                    ProtectedData.Protect(
                        Encoding.UTF8.GetBytes(v),
                        null,
                        protectionScope.Value));
            }

            Write(name, v);
            if (AutoSaveOnSet)
                Save();
        }

        public override void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (typeof(TData) == typeof(string))
                SetInternal(name, (string)(object)value, protectionScope);
            else
                SetInternal(name, JsonConvert.SerializeObject(value), protectionScope);
        }
    }
}
