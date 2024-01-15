using System;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Tentacle.Configuration
{
    /// <summary>
    /// See https://github.com/OctopusDeploy/Configuration/blob/master/source/Octopus.Configuration/IWritableKeyValueStore.cs
    ///
    /// The set methods in this class all return true, because Set is supported.
    /// </summary>
    public abstract class KeyValueStoreBase : IWritableKeyValueStore
    {
        protected readonly bool AutoSaveOnSet;

        protected KeyValueStoreBase(bool autoSaveOnSet)
        {
            Console.WriteLine($"KeyValueStoreBase Created! {GetType().Name}");
            AutoSaveOnSet = autoSaveOnSet;
        }

        protected abstract void Delete(string key);

        [Obsolete("Please use the generic overload instead")]
        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
            => Get(name, default(string), protectionLevel);

        [return: NotNullIfNotNull("defaultValue")]
        public abstract TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None);

        [Obsolete("Please use the generic overload instead")]
        public bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            Set<string?>(name, value, protectionLevel);
            return true;
        }

        public abstract bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None);

        public bool Remove(string name)
        {
            Delete(name);
            return true;
        }

        public abstract bool Save();
    }
}