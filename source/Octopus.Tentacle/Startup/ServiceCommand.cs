using System;
using System.Collections.Generic;
using System.Reflection;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Startup
{
    public class ServiceCommand : AbstractCommand
    {
        readonly string serviceDescription;
        readonly Assembly assemblyContainingService;
        readonly ApplicationName applicationName;
        readonly IApplicationInstanceStore instanceLocator;
        readonly IApplicationInstanceSelector instanceSelector;
        readonly ServiceConfigurationState serviceConfigurationState;
        readonly IServiceConfigurator serviceConfigurator;
        private readonly ISystemLog log;
        readonly string ServicePasswordEnvVar = "OCTOPUS_SERVICE_PASSWORD";
        readonly string ServiceUsernameEnvVar = "OCTOPUS_SERVICE_USERNAME";

        string? instanceName;

        public ServiceCommand(ApplicationName applicationName,
            IApplicationInstanceStore instanceLocator,
            IApplicationInstanceSelector instanceSelector,
            string serviceDescription,
            Assembly assemblyContainingService,
            IServiceConfigurator serviceConfigurator,
            ISystemLog log,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(logFileOnlyLogger)
        {
            this.applicationName = applicationName;
            this.instanceLocator = instanceLocator;
            this.instanceSelector = instanceSelector;
            this.serviceDescription = serviceDescription;
            this.assemblyContainingService = assemblyContainingService;
            this.serviceConfigurator = serviceConfigurator;
            this.log = log;

            serviceConfigurationState = new ServiceConfigurationState
            {
                Username = Environment.GetEnvironmentVariable(ServiceUsernameEnvVar),
                Password = Environment.GetEnvironmentVariable(ServicePasswordEnvVar)
            };

            Options.Add("start", $"Start the service if it is not already running", v => serviceConfigurationState.Start = true);
            Options.Add("stop", $"Stop the service if it is running", v => serviceConfigurationState.Stop = true);
            Options.Add("restart", $"Restart the service if it is running", v => serviceConfigurationState.Restart = true);
            Options.Add("reconfigure", $"Reconfigure the service", v => serviceConfigurationState.Reconfigure = true);
            Options.Add("install", $"Install the service", v => serviceConfigurationState.Install = true);
            Options.Add("username=|user=", $"Username to run the service under (DOMAIN\\Username format for Windows). Only used when --install or --reconfigure are used.  Can also be passed via an environment variable {ServiceUsernameEnvVar}. Defaults to 'root' for Systemd services.", v => serviceConfigurationState.Username = v);
            Options.Add("uninstall", $"Uninstall the service", v => serviceConfigurationState.Uninstall = true);
            Options.Add("password=",
                $"Password for the username specified with --username. Only used when --install or --reconfigure are used. Can also be passed via an environment variable {ServicePasswordEnvVar}.",
                v =>
                {
                    serviceConfigurationState.Password = v;
                },
                sensitive: true);
            Options.Add("dependOn=", "", v => serviceConfigurationState.DependOn = v);
            Options.Add("instance=", "Name of the instance to use, or * to use all instances", v => instanceName = v);
        }

        protected override void Start()
        {
            var exePath = assemblyContainingService.FullProcessPath();

            if (instanceName == "*")
            {
                if (serviceConfigurationState.Reconfigure || serviceConfigurationState.Install || serviceConfigurationState.Uninstall)
                    throw new ControlledFailureException("--instance=* can only be used for --start, --stop, and --restart flags");

                var exceptions = new List<Exception>();

                foreach (var instance in instanceLocator.ListInstances())
                {
                    try
                    {
                        var thisServiceName = ServiceName.GetWindowsServiceName(applicationName, instance.InstanceName);
                        serviceConfigurator.ConfigureServiceByInstanceName(thisServiceName,
                            exePath,
                            instance.InstanceName,
                            serviceDescription,
                            serviceConfigurationState);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                if (exceptions.Count > 0)
                    throw new AggregateException(exceptions);
            }
            else
            {
                var currentName = instanceSelector.Current.InstanceName;
                var thisServiceName = ServiceName.GetWindowsServiceName(applicationName, currentName);
                if (currentName == null)
                {
                    if (serviceConfigurationState.Install || serviceConfigurationState.Reconfigure)
                    {
                        log.Warn("Please note, currently there can only be one un-named instance configured as a service on a machine at a time.");    
                    }
                    serviceConfigurator.ConfigureServiceByConfigPath(thisServiceName,
                        exePath,
                        instanceSelector.Current.ConfigurationPath!,
                        serviceDescription,
                        serviceConfigurationState);
                }
                else
                {
                    serviceConfigurator.ConfigureServiceByInstanceName(thisServiceName,
                        exePath,
                        currentName,
                        serviceDescription,
                        serviceConfigurationState);
                }
            }
        }
    }
}
