using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceRepository : IApplicationInstanceRepository
    {
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        public IList<ApplicationInstance> ListInstances(ApplicationName name)
        {
            var results = new List<ApplicationInstance>();

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
                            results.Add(new ApplicationInstance(instanceName, name, (string)path));
                        }
                    }
                }
            }

            return results;
        }

        public ApplicationInstance GetInstance(ApplicationName name, string instanceName)
        {
            return ListInstances(name).SingleOrDefault(s => s.InstanceName == instanceName);
        }

        public ApplicationInstance GetDefaultInstance(ApplicationName name)
        {
            return GetInstance(name, ApplicationInstance.GetDefaultInstance(name));
        }

        public void SaveInstance(ApplicationInstance instance)
        {
            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = CreateOrOpenKeyForWrite(rootKey, KeyName))
            using (var applicationNameKey = CreateOrOpenKeyForWrite(subKey, instance.ApplicationName.ToString()))
            using (var instanceKey = CreateOrOpenKeyForWrite(applicationNameKey, instance.InstanceName))
            {
                instanceKey.SetValue("ConfigurationFilePath", instance.ConfigurationFilePath);
                instanceKey.Flush();
            }
        }

        public void DeleteInstance(ApplicationInstance instance)
        {
            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = CreateOrOpenKeyForWrite(rootKey, KeyName))
            using (var applicationNameKey = CreateOrOpenKeyForWrite(subKey, instance.ApplicationName.ToString()))
            {
                applicationNameKey.DeleteSubKey(instance.InstanceName);
                applicationNameKey.Flush();
            }
        }

        static RegistryKey CreateOrOpenKeyForWrite(RegistryKey parent, string keyName)
        {
            using (var subKey = parent.OpenSubKey(keyName, true))
            {
                if (subKey == null)
                {
                    parent.CreateSubKey(keyName);
                    parent.Flush();
                }
            }

            return parent.OpenSubKey(keyName, true);
        }
    }
}