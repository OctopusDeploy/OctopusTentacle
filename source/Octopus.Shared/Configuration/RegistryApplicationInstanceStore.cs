using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Octopus.Shared.Configuration
{
    public class RegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        public List<ApplicationInstanceRecord> GetListFromRegistry(ApplicationName name)
        {
            var results = new List<ApplicationInstanceRecord>();

            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = rootKey.OpenSubKey(KeyName, false))
            {
                if (subKey == null)
                    return results;

                using (var applicationNameKey = subKey.OpenSubKey(name.ToString(), false))
                {
                    if (applicationNameKey == null)
                        return results;

                    var instanceNames = applicationNameKey.GetSubKeyNames();

                    foreach (var instanceName in instanceNames)
                    {
                        using (var instanceKey = applicationNameKey.OpenSubKey(instanceName, false))
                        {
                            if (instanceKey == null)
                                continue;

                            var path = instanceKey.GetValue("ConfigurationFilePath");
                            results.Add(new ApplicationInstanceRecord(instanceName, name, (string)path));
                        }
                    }
                }
            }

            return results;
        }

        public void DeleteFromRegistry(ApplicationName name)
        {
            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = rootKey.OpenSubKey(KeyName, true))
            {
                if (subKey == null)
                    return;

                subKey.DeleteSubKey(name.ToString(), false);
            }
        }
    }
}