using System;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceRecord
    {
        readonly string instanceName;
        readonly ApplicationName applicationName;
        readonly string configurationFilePath;

        public ApplicationInstanceRecord(string instanceName, ApplicationName applicationName, string configurationFilePath)
        {
            this.instanceName = instanceName;
            this.applicationName = applicationName;
            this.configurationFilePath = configurationFilePath;
        }

        public string InstanceName
        {
            get { return instanceName; }
        }

        public ApplicationName ApplicationName
        {
            get { return applicationName; }
        }

        public string ConfigurationFilePath
        {
            get { return configurationFilePath; }
        }

        public static string GetDefaultInstance(ApplicationName application)
        {
            return application.ToString();
        }
    }
}