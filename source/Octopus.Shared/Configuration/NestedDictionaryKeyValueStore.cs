using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Octopus.Shared.Configuration
{
    public abstract class NestedDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        public NestedDictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        public override TData Get<TData>(string name, TData defaultValue = default(TData), DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var s = Read(name);
            var value = s as string;
            if (value != null && string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (protectionScope != null)
            {
                if (!(s is string || s is byte[]))
                    throw new InvalidOperationException("Can only decrypt a value stored as string");

                s = Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(
                        Convert.FromBase64String(value),
                        null,
                        protectionScope.Value));
            }
            if (s == null)
                return defaultValue;

            return (TData)s;
        }

        public override void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var s = (object) value as string;
            if (s != null && string.IsNullOrWhiteSpace(s))
            {
                Write(name, null);
                if (autoSaveOnSet)
                    Save();
                return;
            }

            var v = (object) value;

            if (protectionScope != null)
            {
                if (!((object) value is string))
                    v = JsonConvert.SerializeObject(value);
                v = Convert.ToBase64String(
                    ProtectedData.Protect(
                        Encoding.UTF8.GetBytes((string)v),
                        null,
                        protectionScope.Value));
            }

            Write(name, v);
            if (autoSaveOnSet)
                Save();
        }
    }
}
