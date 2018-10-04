using System.Collections.Generic;
using Microsoft.Win32;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceStore : IApplicationInstanceStore
    {
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        public IList<ApplicationInstanceRecord> ListInstances(ApplicationName name)
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

        public void SaveInstance(ApplicationInstanceRecord instanceRecord)
        {
            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = CreateOrOpenKeyForWrite(rootKey, KeyName))
            using (var applicationNameKey = CreateOrOpenKeyForWrite(subKey, instanceRecord.ApplicationName.ToString()))
            using (var instanceKey = CreateOrOpenKeyForWrite(applicationNameKey, instanceRecord.InstanceName))
            {
                instanceKey.SetValue("ConfigurationFilePath", instanceRecord.ConfigurationFilePath);
                instanceKey.Flush();
            }
        }

        public void DeleteInstance(ApplicationInstanceRecord instanceRecord)
        {
            using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
            using (var subKey = CreateOrOpenKeyForWrite(rootKey, KeyName))
            using (var applicationNameKey = CreateOrOpenKeyForWrite(subKey, instanceRecord.ApplicationName.ToString()))
            {
                applicationNameKey.DeleteSubKey(instanceRecord.InstanceName);
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