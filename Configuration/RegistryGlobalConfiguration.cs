using System;
using Microsoft.Win32;

namespace Octopus.Shared.Configuration
{
    public class RegistryGlobalConfiguration : IGlobalConfiguration
    {
        public string Get(string name)
        {
            var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            key = key.OpenSubKey("Software\\Octopus", false);
            if (key != null)
            {
                return (string) key.GetValue(name, null);
            }

            return null;
        }

        public void Set(string name, string value)
        {
            var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var octopusKey = key.OpenSubKey("Software\\Octopus", true);
            if (octopusKey == null)
            {
                key.CreateSubKey("Software\\Octopus");
                octopusKey = key.OpenSubKey("Software\\Octopus", true);
            }

            octopusKey.SetValue(name, value);
            octopusKey.Flush();
            octopusKey.Close();
        }
    }
}