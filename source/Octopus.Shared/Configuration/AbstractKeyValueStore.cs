using System;
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
        public string? Get(string name, ProtectionLevel protectionLevel  = ProtectionLevel.None)
        {
            return Get(name, default(string), protectionLevel) ?? string.Empty;
        }

        public abstract TData Get<TData>(string name, TData defaultValue, ProtectionLevel protectionLevel = ProtectionLevel.None);

        [Obsolete("Please use the generic overload instead")]
        public void Set(string name, string? value, ProtectionLevel protectionLevel  = ProtectionLevel.None)
        {
            Set<string?>(name, value, protectionLevel);
        }

        public abstract void Set<TData>(string name, TData value, ProtectionLevel protectionLevel  = ProtectionLevel.None);

        public void Remove(string name)
        {
            Delete(name);
        }

        public abstract void Save();
    }
}