using System;

namespace Octopus.Shared.Configuration.Instances
{
    public class ApplicationRecord
    {
    }

    public class ApplicationInstanceRecord : ApplicationRecord
    {
        public ApplicationInstanceRecord(string instanceName, string configurationFilePath, bool isDefaultInstance)
        {
            InstanceName = instanceName;
            ConfigurationFilePath = configurationFilePath;
            IsDefaultInstance = isDefaultInstance;
        }

        public string InstanceName { get; }

        public string ConfigurationFilePath { get; }

        public bool IsDefaultInstance { get; }

        public static string GetDefaultInstance(ApplicationName application)
        {
            return application.ToString();
        }
    }
}