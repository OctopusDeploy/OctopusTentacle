using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Win32;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Instances
{
    /// <summary>
    /// This is here for legacy purposes, we need it to read the old entries in order to migrate them to the new file based index.
    /// </summary>
    [SuppressMessage("Usage", "PC001:API not supported on all platforms")]
    class WindowsRegistryApplicationInstanceStore : IRegistryApplicationInstanceStore
    {
#pragma warning disable CA1416
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
#pragma warning restore CA1416
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

#pragma warning disable CA1416
            using var rootKey = RegistryKey.OpenBaseKey(Hive, View);
            using var subKey = rootKey.OpenSubKey(KeyName, false);
            if (subKey == null)
                return results;

            using var applicationNameKey = subKey.OpenSubKey(applicationName.ToString(), false);
            if (applicationNameKey == null)
                return results;

            var instanceNames = applicationNameKey.GetSubKeyNames();

            foreach (var instanceName in instanceNames)
            {
                using var instanceKey = applicationNameKey.OpenSubKey(instanceName, false);
                var path = instanceKey?.GetValue("ConfigurationFilePath");
#pragma warning restore CA1416
                if (path is null)
                {
                    continue;
                }
                results.Add(new ApplicationInstanceRecord(instanceName, (string)path));
            }

            return results;
        }

        public void DeleteFromRegistry(string instanceName)
        {
            log.Info($"Entering DeleteFromRegistry");

            if (!PlatformDetection.IsRunningOnWindows)
            {
                log.Info($"Inside DeleteFromRegistry: Is running on Windows");
                return;
            }

            var registryInstance = GetInstanceFromRegistry(instanceName);
            if (registryInstance == null)
            {
                log.Info($"Inside DeleteFromRegistry: registryInstance looks like is null");
                return;
            }

            try
            {
#pragma warning disable CA1416
                log.Info($"Inside DeleteFromRegistry: trying to delete from actual registry");
                using var rootKey = RegistryKey.OpenBaseKey(Hive, View);
                using var subKey = rootKey.OpenSubKey(KeyName, true);

                using var applicationNameKey = subKey?.OpenSubKey(applicationName.ToString(), true);
                if (applicationNameKey == null)
                {
                    log.Info($"Inside DeleteFromRegistry: No applicationNameKey found: {applicationNameKey}");
                    return;
                }

                applicationNameKey.DeleteSubKey(instanceName);
                log.Info($"Inside DeleteFromRegistry: could delete from actual registry");
                applicationNameKey.Flush();
                log.Info($"Inside DeleteFromRegistry: flushed");
#pragma warning restore CA1416
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
