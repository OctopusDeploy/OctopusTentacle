using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Octopus.Shared.Configuration
{
    public class RegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        public ApplicationInstanceRecord GetInstanceFromRegistry(ApplicationName name, string instanceName)
        {
            var allInstances = GetListFromRegistry(name);
            return allInstances.SingleOrDefault(i => i.InstanceName.Equals(instanceName, StringComparison.CurrentCultureIgnoreCase));
        }

        public IEnumerable<ApplicationInstanceRecord> GetListFromRegistry(ApplicationName name)
        {
            var results = new List<ApplicationInstanceRecord>();

#if FULL_FRAMEWORK
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
#endif

            return results;
        }



        public void DeleteFromRegistry(ApplicationName name, string instanceName)
        {
#if FULL_FRAMEWORK
            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = rootKey.OpenSubKey(KeyName, true))
            {
                if (subKey == null)
                    return;

                using (var applicationNameKey = subKey.OpenSubKey(name.ToString(), true))
                {
                    if (applicationNameKey == null)
                        return;

                    applicationNameKey.DeleteSubKey(instanceName);
                    applicationNameKey.Flush();
                }
            }
#endif
        }
    }
}