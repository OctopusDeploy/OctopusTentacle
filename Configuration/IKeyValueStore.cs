using System;
using System.Security.Cryptography;

namespace Octopus.Shared.Configuration
{
    public interface IKeyValueStore
    {
        string Get(string name, DataProtectionScope? protectionScope = null);
        TData Get<TData>(string name, TData defaultValue = default(TData), DataProtectionScope? protectionScope = null);
        void Set(string name, string value, DataProtectionScope? protectionScope = null);
        void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null);
        void Save();
    }
}