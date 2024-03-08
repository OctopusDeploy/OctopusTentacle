using System;
using System.Diagnostics;
using System.IO;
using Autofac;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration
{
    public class TentacleManagerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            
            builder.RegisterType<InstanceSelectionModel>().AsSelf().SingleInstance().WithParameter("applicationName", ApplicationName.Tentacle);
            builder.RegisterType<CommandLineRunner>().As<ICommandLineRunner>();
            builder.RegisterType<TelemetryService>().As<ITelemetryService>();

            RegisterTentacleManagerInstanceIdentifierService(builder);

            // View Model registration
            builder.RegisterType<TentacleManagerModel>();
            builder.RegisterType<DeleteWizardModel>().AsSelf();
            builder.RegisterType<ProxyWizardModel>().AsSelf();
            builder.RegisterType<PollingProxyWizardModel>().AsSelf();
            builder.RegisterType<SetupTentacleWizardModel>().AsSelf();
        }

        static void RegisterTentacleManagerInstanceIdentifierService(ContainerBuilder builder)
        {
            string processExecutableLocation;
            using (var currentProcess = Process.GetCurrentProcess())
            {
                processExecutableLocation = Directory.GetParent(currentProcess.MainModule.FileName).FullName;
            }
            
            builder.RegisterType<TentacleManagerInstanceIdentifierService>()
                .As<ITentacleManagerInstanceIdentifierService>()
                .SingleInstance()
                .WithParameter("identifierFilePath", Path.Combine(processExecutableLocation, "TentacleManagerInstanceID"));
        }
    }
}
