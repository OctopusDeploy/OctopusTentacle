using Autofac;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using InstanceSelectionModel = Octopus.Manager.Tentacle.Shell.InstanceSelectionModel;
using ShellViewModel = Octopus.Manager.Tentacle.Shell.ShellViewModel;

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

        static ShellView CreateShell(IComponentContext container)
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
                    container.Resolve<IApplicationInstanceLocator>(),
                    container.Resolve<IApplicationInstanceManager>(),
                    newInstanceLauncher,
                    container.Resolve<ProxyWizardLauncher>(),
                    container.Resolve<DeleteWizardLauncher>()));
            return shell;
        }
    }
}