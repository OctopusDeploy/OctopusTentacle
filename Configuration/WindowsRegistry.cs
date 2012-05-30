using System;
using Microsoft.Win32;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Makes it easy to store key/value pairs in the Windows Registry.
    /// </summary>
    public class WindowsRegistry : IWindowsRegistry
    {
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        public TData Get<TData>(string name, TData defaultValue)
        {
            var value = GetString(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return (TData)Convert.ChangeType(value, typeof (TData));
            }

            return defaultValue;
        }

        public void Set(string name, object value)
        {
            SetString(name, value == null ? null : value.ToString());
        }

        public string GetString(string name)
        {
            var key = RegistryKey.OpenBaseKey(Hive, View);
            key = key.OpenSubKey(KeyName, false);
            if (key != null)
            {
                return (string) key.GetValue(name, null);
            }

            return null;
        }

        void SetString(string name, string value)
        {
            var key = RegistryKey.OpenBaseKey(Hive, View);
            var octopusKey = key.OpenSubKey(KeyName, true);
            if (octopusKey == null)
            {
                key.CreateSubKey(KeyName);
                octopusKey = key.OpenSubKey(KeyName, true);
            }

            octopusKey.SetValue(name, value);
            octopusKey.Flush();
            octopusKey.Close();
        }
    }
}