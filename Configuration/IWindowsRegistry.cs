using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Makes it easy to store key/value pairs in the Windows Registry.
    /// </summary>
    public interface IWindowsRegistry
    {
        TData Get<TData>(string name, TData defaultValue);
        void Set(string name, object value);
        string GetString(string name);
    }
}