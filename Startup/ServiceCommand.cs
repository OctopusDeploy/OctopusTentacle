#if WINDOWS_SERVICE
using System;
using System.Reflection;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class ServiceCommand : AbstractStandardCommand
    {
        readonly ILog log;
        readonly string serviceDescription;
        readonly Assembly assemblyContainingService;
        readonly IApplicationInstanceSelector instanceSelector;
        readonly ServiceConfigurationState serviceConfigurationState;

        public ServiceCommand(IApplicationInstanceSelector instanceSelector, string serviceDescription, Assembly assemblyContainingService, ILog log) : base(instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            this.serviceDescription = serviceDescription;
            this.assemblyContainingService = assemblyContainingService;
            this.log = log;

            serviceConfigurationState = new ServiceConfigurationState();

            Options.Add("start", "Start the Windows Service if it is not already running", v => serviceConfigurationState.Start = true);
            Options.Add("stop", "Stop the Windows Service if it is running", v => serviceConfigurationState.Stop = true);
            Options.Add("reconfigure", "Reconfigure the Windows Service", v => serviceConfigurationState.Reconfigure = true);
            Options.Add("install", "Install the Windows Service", v => serviceConfigurationState.Install = true);
            Options.Add("username=", "Username to run the service under (DOMAIN\\Username format). Only used when --install is used.", v => serviceConfigurationState.Username = v);
            Options.Add("uninstall", "Uninstall the Windows Service", v => serviceConfigurationState.Uninstall = true);
            Options.Add("password=", "Password for the username specified with --username. Only used when --install is used.", v =>
            {
                serviceConfigurationState.Password = v;
            });
            Options.Add("dependOn=", "", v => serviceConfigurationState.DependOn = v);
        }

        protected override void Start()
        {
            base.Start();

            var thisServiceName = ServiceName.GetWindowsServiceName(instanceSelector.GetCurrentInstance().ApplicationName, instanceSelector.GetCurrentInstance().InstanceName);
            var instance = instanceSelector.GetCurrentInstance().InstanceName;
            var exePath = assemblyContainingService.FullLocalPath();

            var serverInstaller = new ConfigureServiceHelper(log, thisServiceName, exePath, instance, serviceDescription, serviceConfigurationState);

            serverInstaller.ConfigureService();
        }
    }
}
#endif