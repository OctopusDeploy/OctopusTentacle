using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Startup
{
    public interface IServiceConfigurator
    {
        Task ConfigureServiceByInstanceNameAsync(string thisServiceName,
            string exePath,
            string instance,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState);

        Task ConfigureServiceByConfigPathAsync(string thisServiceName,
            string exePath,
            string configPath,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState);
    }
}