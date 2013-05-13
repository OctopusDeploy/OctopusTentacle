using System;
using Autofac;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        public string OctopusConfigurationFile { get; set; }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            if (!string.IsNullOrWhiteSpace(OctopusConfigurationFile))
            {
                Logger.Default.Debug("Octopus configuration will be read from: " + OctopusConfigurationFile);
                builder.RegisterInstance(XmlOctopusConfiguration.LoadFrom(OctopusConfigurationFile)).As<IOctopusConfiguration>();
            }
            else
            {
                Logger.Default.Debug("Octopus configuration will be read from the registry");
                builder.RegisterType<RegistryOctopusConfiguration>().As<IOctopusConfiguration>();
            }

            builder.RegisterType<WindowsRegistry>().As<IWindowsRegistry>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>().As<IStartable>();
            builder.RegisterType<RegistryTentacleConfiguration>().As<ITentacleConfiguration>();
        }
    }
}