using System;

namespace Octopus.Shared.Startup
{
    public interface IServiceConfigurator
    {
        void ConfigureServiceByInstanceName(string thisServiceName,
            string exePath,
            string instance,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState);
        
        void ConfigureServiceByConfigPath(string thisServiceName,
            string exePath,
            string configPath,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState);
    }
}