using System;

namespace Octopus.Shared.Configuration.Instances
{
    public class ApplicationInstanceRecord
    {
        public ApplicationInstanceRecord(string instanceName, bool isDefaultInstance)
        {
            InstanceName = instanceName;
            IsDefaultInstance = isDefaultInstance;
        }

        public string InstanceName { get; }

        public bool IsDefaultInstance { get; }
    }

    public class PersistedApplicationInstanceRecord : ApplicationInstanceRecord
    {
        public PersistedApplicationInstanceRecord(string instanceName, string configurationFilePath, bool isDefaultInstance) : base(instanceName, isDefaultInstance)
        {
            ConfigurationFilePath = configurationFilePath;
        }
 
        public string ConfigurationFilePath { get; }

        public static string GetDefaultInstance(ApplicationName application)
        {
            return application.ToString();
        }
    }
}