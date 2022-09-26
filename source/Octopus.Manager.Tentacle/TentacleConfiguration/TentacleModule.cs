using System;
using Autofac;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Manager.Tentacle.TentacleConfiguration
{
    public class TentacleModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ShellViewModel>().OnActivating(e => e.Instance.ShowEAPVersion = false);
            builder.Register(CreateShell).As<ShellView>();
            builder.RegisterType<TentacleManagerModel>();
            builder.RegisterType<TentacleSetupWizardLauncher>();
            builder.RegisterType<ProxyWizardLauncher>();
            builder.RegisterType<DeleteWizardLauncher>();
            builder.RegisterType<InstanceSelectionModel>().AsSelf().SingleInstance().WithParameter("applicationName", ApplicationName.Tentacle);
        }

        private static ShellView CreateShell(IComponentContext container)
        {
            var newInstanceLauncher = container.Resolve<TentacleSetupWizardLauncher>();

            var shellModel = container.Resolve<ShellViewModel>();
            var shell = new ShellView("Tentacle Manager", shellModel);
            shell.EnableInstanceSelection();
            shell.Height = 550;
            shell.SetViewContent(
                new TentacleManagerView(
                    container.Resolve<TentacleManagerModel>(),
                    container.Resolve<InstanceSelectionModel>(),
                    container.Resolve<IApplicationInstanceManager>(),
                    container.Resolve<IApplicationInstanceStore>(),
                    newInstanceLauncher,
                    container.Resolve<ProxyWizardLauncher>(),
                    container.Resolve<DeleteWizardLauncher>()));
            return shell;
        }
    }
}