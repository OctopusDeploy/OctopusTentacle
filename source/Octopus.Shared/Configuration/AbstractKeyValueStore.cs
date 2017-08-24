using System;
using System.Security.Cryptography;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{

    public abstract class AbstractKeyValueStore : IKeyValueStore
    {
        protected readonly bool autoSaveOnSet;
        protected abstract void Write(string key, object value);
        protected abstract object Read(string key);
        protected abstract void Delete(string key);

        protected AbstractKeyValueStore(bool autoSaveOnSet)
        {
            this.autoSaveOnSet = autoSaveOnSet;
        }

        [Obsolete("Please use the generic overload instead")]
        public string Get(string name, DataProtectionScope? protectionScope = null)
        {
            return Get<string>(name);
        }

        public abstract TData Get<TData>(string name, TData defaultValue = default(TData),
            DataProtectionScope? protectionScope = null);

        [Obsolete("Please use the generic overload instead")]
        public void Set(string name, string value, DataProtectionScope? protectionScope = null)
        {
            Set<string>(name, value, protectionScope);
        }

        public abstract void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null);
       
        public void Remove(string name)
        {
            Delete(name);
        }

        public abstract void Save();
    }
}