using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    class DoNotAllowWritesInThisModeKeyValueStore : IWritableKeyValueStore
    {
        readonly IKeyValueStore readonlyStore;

        public DoNotAllowWritesInThisModeKeyValueStore(IKeyValueStore readonlyStore)
        {
            this.readonlyStore = readonlyStore;
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
            => readonlyStore.Get(name, protectionLevel);

        public TData Get<TData>(string name, TData defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None)
            => readonlyStore.Get(name, defaultValue, protectionLevel);

        public bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
            => throw new InvalidOperationException($"The setting {name} cannot be written in this startup mode, you must specify an instance name or a config file");

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
            => throw new InvalidOperationException($"The setting {name} cannot be written in this startup mode, you must specify an instance name or a config file");

        public bool Remove(string name)
            => throw new InvalidOperationException($"The setting {name} cannot be removed in this startup mode, you must specify an instance name or a config file");

        public bool Save()
            => throw new InvalidOperationException("The settings cannot be saved in this startup mode, you must specify an instance name or a config file");
    }
}