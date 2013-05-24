using System;

namespace Octopus.Shared.Configuration
{
    public interface IKeyValueStore
    {
        TData Get<TData>(string name, TData defaultValue);
        string Get(string name);
        string GetSecure(string name);

        void Set(string name, object value);
        void SetSecure(string name, object value);

        void Save();
    }
}