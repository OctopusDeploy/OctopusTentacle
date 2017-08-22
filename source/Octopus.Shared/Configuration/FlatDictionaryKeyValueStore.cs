using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Octopus.Shared.Configuration
{
    public abstract class FlatDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        public FlatDictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        public override TData Get<TData>(string name, TData defaultValue = default(TData), DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var s = Read(name);
            if (s == null)
                return defaultValue;

            var value = s as string;
            if (value != null && string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (protectionScope != null)
            {
                if (!(s is string))
                    throw new InvalidOperationException("Cannot decrypt value for " + name + ", as its not stored as string. The value is stored as " + s.GetType() + ".");

                s = Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(
                        Convert.FromBase64String(value),
                        null,
                        protectionScope.Value));
            }
            if (s == null)
                return defaultValue;

            if (typeof(TData) == typeof(string))
                return (TData)s;

            return JsonConvert.DeserializeObject<TData>((string)s);
        }

        private void Set(string name, string value, DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (string.IsNullOrWhiteSpace(value))
            {
                Write(name, null);
                if (autoSaveOnSet)
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
            if (autoSaveOnSet)
                Save();
        }

        public override void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (typeof(TData) == typeof(string))
                Set(name, (string)(object)value, protectionScope);
            else
                Set(name, JsonConvert.SerializeObject(value), protectionScope);
        }
    }
}
