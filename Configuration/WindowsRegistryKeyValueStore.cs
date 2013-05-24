using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Makes it easy to store key/value pairs in the Windows Registry.
    /// </summary>
    public class WindowsRegistryKeyValueStore : DictionaryKeyValueStore
    {
        readonly ILog log;
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        public WindowsRegistryKeyValueStore(ILog log)
        {
            this.log = log;
        }

        protected override void LoadSettings(IDictionary<string, string> settingsToFill)
        {
            log.Info("Loading configuration settings from the Windows registry...");

            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = rootKey.OpenSubKey(KeyName, false))
            {
                if (subKey == null)
                    return;

                var valueNames = subKey.GetValueNames();

                foreach (var valueName in valueNames)
                {
                    var value = (string)subKey.GetValue(valueName);
                    settingsToFill[valueName] = value;
                }
            }
        }

        protected override void SaveSettings(IDictionary<string, string> settingsToSave)
        {
            log.Info("Saving configuration settings to the Windows registry...");
            
            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            {
                using (var subKey = rootKey.OpenSubKey(KeyName, true))
                {
                    if (subKey == null)
                    {
                        rootKey.CreateSubKey(KeyName);
                        rootKey.Flush();
                        rootKey.Close();
                    }
                }

                using (var subKey = rootKey.OpenSubKey(KeyName, true))
                {
                    if (subKey == null)
                        throw new Exception("The settings key HKLM\\" + KeyName + " could not be created");

                    foreach (var setting in settingsToSave)
                    {
                        subKey.SetValue(setting.Key, setting.Value);
                        subKey.Flush();
                        subKey.Close();
                    }
                }
            }
        }
    }
}