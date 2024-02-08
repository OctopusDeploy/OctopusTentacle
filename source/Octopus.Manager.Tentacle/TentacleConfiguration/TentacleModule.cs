using System;
using Autofac;
using Octopus.Client.Model;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration
{
    public class TentacleModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // TODO: Remove ShellViewModel registration
            // Views should depend on their specific view model,
            // which will likely *derive* from ShellViewModel
            builder.RegisterType<ShellViewModel>();
            
            builder.RegisterType<TentacleManagerModel>();
            builder.RegisterType<TentacleSetupWizardLauncher>();
            builder.RegisterType<ProxyWizardLauncher>();
            builder.RegisterType<InstanceSelectionModel>().AsSelf().SingleInstance().WithParameter("applicationName", ApplicationName.Tentacle);
            builder.RegisterType<CommandLineRunner>().As<ICommandLineRunner>();

            // View Model registration
            builder.RegisterType<DeleteWizardModel>().AsSelf();
            builder.RegisterType<ProxyWizardModel>().AsSelf();
            builder.RegisterType<PollingProxyWizardModel>().AsSelf();
        }
    }
}
