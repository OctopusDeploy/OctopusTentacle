using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Commands
{
    public class RestartInstancesCommand : AbstractCommand
    {
        readonly IApplicationInstanceStore instanceStore;
        readonly IServiceConfigurator serviceConfigurator;

        public RestartInstancesCommand(IApplicationInstanceStore instanceStore, IServiceConfigurator serviceConfigurator)
        {
            this.instanceStore = instanceStore;
            this.serviceConfigurator = serviceConfigurator;
        }

        protected override void Start()
        {
            var instances = instanceStore.ListInstances(ApplicationName.Tentacle);
            var stopConfig = new ServiceConfigurationState() { Stop = true };
            var startConfig = new ServiceConfigurationState() { Start = true };

            foreach (var instance in instances)
            {
                try
                {
                    var serviceName = PlatformDetection.IsRunningOnWindows
                        ? ServiceName.GetWindowsServiceName(ApplicationName.Tentacle, instance.InstanceName)
                        : "";

                    serviceConfigurator.ConfigureService(serviceName, "", instance.InstanceName, "", stopConfig);
                    serviceConfigurator.ConfigureService(serviceName, "", instance.InstanceName, "", startConfig);
                }
                catch
                {
                    // Something went wrong, but the serviceConfigurator has already logged it.
                    // So don't log anything here. Just move on to the next instance.
                }
            }
        }
    }
}
