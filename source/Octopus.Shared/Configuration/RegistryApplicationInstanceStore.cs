using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class RegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        readonly ApplicationName applicationName;
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        public RegistryApplicationInstanceStore(ApplicationName applicationName)
        {
            this.applicationName = applicationName;
        }

        public ApplicationInstanceRecord GetInstanceFromRegistry(string instanceName)
        {
            var allInstances = GetListFromRegistry();
            return allInstances.SingleOrDefault(i => i.InstanceName.Equals(instanceName, StringComparison.CurrentCultureIgnoreCase));
        }

        public IEnumerable<ApplicationInstanceRecord> GetListFromRegistry()
        {
            var results = new List<ApplicationInstanceRecord>();

            if (PlatformDetection.IsRunningOnWindows)
            {
                using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
                using (var subKey = rootKey.OpenSubKey(KeyName, false))
                {
                    if (subKey == null)
                        return results;

                    using (var applicationNameKey = subKey.OpenSubKey(applicationName.ToString(), false))
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
                                results.Add(new ApplicationInstanceRecord(instanceName, (string)path));
                            }
                        }
                    }
                }
            }

            return results;
        }

        public void DeleteFromRegistry(string instanceName)
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
                using (var subKey = rootKey.OpenSubKey(KeyName, true))
                {
                    if (subKey == null)
                        return;

                    using (var applicationNameKey = subKey.OpenSubKey(applicationName.ToString(), true))
                    {
                        if (applicationNameKey == null)
                            return;

                        applicationNameKey.DeleteSubKey(instanceName);
                        applicationNameKey.Flush();
                    }
                }
            }
        }
    }
}