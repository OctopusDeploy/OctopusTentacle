using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
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

        public abstract TData Get<TData>(string name, TData defaultValue = default(TData),
            DataProtectionScope? protectionScope = null);

        public abstract void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null);
       
        public void Remove(string name)
        {
            Delete(name);
        }

        public abstract void Save();
    }
}