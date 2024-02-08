using System.Windows;
using Autofac;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Proxy
{
    public class ProxyWizardLauncher
    {
        readonly IComponentContext container;

        public ProxyWizardLauncher(IComponentContext container)
        {
            this.container = container;
        }

        public bool? ShowDialog(Window owner, TentacleManagerModel tentacleManagerModel)
        {
            var wizard = new TabbedWizard();

            var wizardModel = tentacleManagerModel.GetProxyWizardModel(tentacleManagerModel.ProxyConfiguration);
            wizard.AddTab(new ProxyConfigurationTab(wizardModel));
            var wrapper = new ProxyWizardModelWrapper(wizardModel);

            if (tentacleManagerModel.PollingProxyConfiguration != null)
            {
                var pollingWizardModel = tentacleManagerModel.GetProxyWizardModel(tentacleManagerModel.PollingProxyConfiguration);
                wizard.AddTab(new ProxyConfigurationTab(pollingWizardModel));
                wrapper.AddPollingModel(pollingWizardModel);
            }

            wizard.AddTab(new InstallTab(wrapper, container.Resolve<ICommandLineRunner>()) {ReadyMessage = "That's all the information we need. When you click the button below, your proxy settings will be saved and the service will be restarted.", SuccessMessage = "Happy deployments!", ExecuteButtonText = "APPLY", Title = "Apply", Header = "Apply"});

            return BuildShell(owner, wizard).ShowDialog();
        }

        ShellView BuildShell(Window owner, TabbedWizard wizard)
        {
            var shellModel = container.Resolve<ShellViewModel>();
            var shell = new ShellView("Proxy Configuration Wizard", shellModel)
            {
                Height = 590,
                Width = 890
            };
            shell.SetViewContent(wizard);
            shell.Owner = owner;
            return shell;
        }
    }
}