using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public abstract class AbstractKeyValueStore : IKeyValueStore
    {
        readonly bool autoSaveOnSet;
        protected abstract void Write(string key, string value);
        protected abstract string Read(string key);
        protected abstract void Delete(string key);

        protected AbstractKeyValueStore(bool autoSaveOnSet)
        {
            this.autoSaveOnSet = autoSaveOnSet;
        }

        public string Get(string name, DataProtectionScope? protectionScope = null)
        {
            var s = Read(name);
            if (string.IsNullOrWhiteSpace(s))
                return null;

            if (protectionScope != null)
            {
                s = Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(
                        Convert.FromBase64String(s),
                        null,
                        protectionScope.Value));
            }

            return s;
        }

        public TData Get<TData>(string name, TData defaultValue = default(TData), DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var s = Get(name, protectionScope);
            if (s == null)
                return defaultValue;

            if (typeof (TData) == typeof (string))
                return (TData)(object)s;

            return JsonConvert.DeserializeObject<TData>(s);
        }

        public void Set(string name, string value, DataProtectionScope? protectionScope = null)
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

        public void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (typeof (TData) == typeof (string))
                Set(name, (string)(object)value, protectionScope);
            else
                Set(name, JsonConvert.SerializeObject(value), protectionScope);
        }

        public void Remove(string name)
        {
            Delete(name);
        }

        public abstract void Save();
    }
}