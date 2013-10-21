using System;
using Octopus.Platform.Deployment.Configuration;

namespace Octopus.Shared.Configuration
{
    public class LoadedApplicationInstance
    {
        readonly ApplicationName applicationName;
        readonly string instanceName;
        readonly IKeyValueStore configuration;

        public LoadedApplicationInstance(ApplicationName applicationName, string instanceName, IKeyValueStore configuration)
        {
            this.applicationName = applicationName;
            this.instanceName = instanceName;
            this.configuration = configuration;
        }

        public string InstanceName
        {
            get { return instanceName; }
        }

        public ApplicationName ApplicationName
        {
            get { return applicationName; }
        }

        public IKeyValueStore Configuration
        {
            get { return configuration; }
        }
    }
}