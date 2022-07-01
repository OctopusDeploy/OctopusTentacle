using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    /// <summary>
    /// This is here for legacy purposes, we need it to read the old entries in order to migrate them to the new file based index.
    /// </summary>
    class WindowsRegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";

        private readonly ApplicationName applicationName;
        readonly ISystemLog log;

        public WindowsRegistryApplicationInstanceStore(ApplicationName applicationName, ISystemLog log)
        {
            this.applicationName = applicationName;
            this.log = log;
        }

        public ApplicationInstanceRecord? GetInstanceFromRegistry(string instanceName)
        {
            var allInstances = GetListFromRegistry();
            return allInstances.SingleOrDefault(i => i.InstanceName.Equals(instanceName, StringComparison.CurrentCultureIgnoreCase));
        }

        public IEnumerable<ApplicationInstanceRecord> GetListFromRegistry()
        {
            var results = new List<ApplicationInstanceRecord>();

            if (!PlatformDetection.IsRunningOnWindows)
                return results;

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

            return results;
        }

        public void DeleteFromRegistry(string instanceName)
        {
            if (!PlatformDetection.IsRunningOnWindows)
                return;

            var registryInstance = GetInstanceFromRegistry(instanceName);
            if (registryInstance == null)
                return;

            try
            {
                using (var rootKey = RegistryKey.OpenBaseKey(Hive, View))
                {
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
            catch (UnauthorizedAccessException ex)
            {
                log.Error(ex, $"Unable to delete instance '{instanceName}' from registry at {Hive}\\{KeyName}\\{applicationName}\\{instanceName} as user '{Environment.UserName}'.");
                throw new ControlledFailureException($"Unable to delete instance '{instanceName}' from registry as user '{Environment.UserName}'. Please check your permissions.");
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Unable to delete instance '{instanceName}' from registry at {Hive}\\{KeyName}\\{applicationName}\\{instanceName} as user '{Environment.UserName}'.");
                throw new ControlledFailureException($"Unable to delete instance '{instanceName}' from registry as user '{Environment.UserName}'.");
            }
        }
    }
}
