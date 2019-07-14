using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class ServiceConfiguratorFactory
    {
        readonly ILog log;

        public ServiceConfiguratorFactory(ILog log)
        {
            this.log = log;
        }

        public IServiceConfigurator GetServiceConfigurator(string thisServiceName, string exePath, string instance, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
        {
            if (!PlatformDetection.IsRunningOnWindows)
            {
                return new WindowsServiceConfigurator(log, thisServiceName, exePath, instance, serviceDescription, serviceConfigurationState);
            }

            return new LinuxServiceConfigurator(log, thisServiceName, exePath, instance, serviceDescription, serviceConfigurationState);
        }
    }
}
