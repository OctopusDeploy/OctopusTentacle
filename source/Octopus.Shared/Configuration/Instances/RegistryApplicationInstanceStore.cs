using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    /// <summary>
    /// This is here for legacy purposes, we need it to read the old entries in order to migrate them to the new file based index.
    /// </summary>
    public class RegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        readonly StartUpInstanceRequest startUpInstanceRequest;

        public RegistryApplicationInstanceStore(StartUpInstanceRequest startUpInstanceRequest)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
        }

        public PersistedApplicationInstanceRecord GetInstanceFromRegistry(string instanceName)
        {
            var allInstances = GetListFromRegistry();
            return allInstances.SingleOrDefault(i => i.InstanceName.Equals(instanceName, StringComparison.CurrentCultureIgnoreCase));
        }

        public IEnumerable<PersistedApplicationInstanceRecord> GetListFromRegistry()
        {
            var results = new List<PersistedApplicationInstanceRecord>();

            if (PlatformDetection.IsRunningOnWindows)
            {
                using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
                using (var subKey = rootKey.OpenSubKey(KeyName, false))
                {
                    if (subKey == null)
                        return results;

                    using (var applicationNameKey = subKey.OpenSubKey(startUpInstanceRequest.ApplicationName.ToString(), false))
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
                                results.Add(new PersistedApplicationInstanceRecord(instanceName, (string)path, instanceName == PersistedApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName)));
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

                    using (var applicationNameKey = subKey.OpenSubKey(startUpInstanceRequest.ApplicationName.ToString(), true))
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