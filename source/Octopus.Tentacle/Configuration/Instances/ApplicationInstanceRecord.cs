using System;

namespace Octopus.Tentacle.Configuration.Instances
{
    public class ApplicationInstanceRecord
    {
        public ApplicationInstanceRecord(string instanceName, string configurationFilePath)
        {
            InstanceName = instanceName;
            ConfigurationFilePath = configurationFilePath;
        }

        public string InstanceName { get; }

        public string ConfigurationFilePath { get; }

        public static string GetDefaultInstance(ApplicationName application) => application.ToString();
    }
}