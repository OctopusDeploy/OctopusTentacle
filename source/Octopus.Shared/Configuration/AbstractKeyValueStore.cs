using System;
using System.Security.Cryptography;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{

    public abstract class AbstractKeyValueStore : IKeyValueStore
    {
        protected readonly bool AutoSaveOnSet;
        protected abstract void Delete(string key);

        protected AbstractKeyValueStore(bool autoSaveOnSet)
        {
            AutoSaveOnSet = autoSaveOnSet;
        }

        [Obsolete("Please use the generic overload instead")]
        public string Get(string name, bool machineKeyEncrypted = false)
        {
            return Get(name, default(string), machineKeyEncrypted);
        }

        public abstract TData Get<TData>(string name, TData defaultValue, bool machineKeyEncrypted = false);

        [Obsolete("Please use the generic overload instead")]
        public void Set(string name, string value, bool machineKeyEncrypted = false)
        {
            Set<string>(name, value, machineKeyEncrypted);
        }

        public abstract void Set<TData>(string name, TData value, bool machineKeyEncrypted = false);
       
        public void Remove(string name)
        {
            Delete(name);
        }

        public abstract void Save();
    }
}