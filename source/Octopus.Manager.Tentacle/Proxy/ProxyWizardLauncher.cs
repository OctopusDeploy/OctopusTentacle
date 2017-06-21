using System.Windows;
using Autofac;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.Proxy
{
    public class ProxyWizardLauncher
    {
        readonly IComponentContext container;

        public ProxyWizardLauncher(IComponentContext container)
        {
            this.container = container;
        }

        public bool? ShowDialog(Window owner, ApplicationName application, string selectedInstance, IProxyConfiguration proxyConfiguration, IProxyConfiguration pollingProxyConfiguration = null)
        {
            var wizard = new TabbedWizard();

            var wizardModel = LoadModel(application, proxyConfiguration, selectedInstance);
            wizard.AddTab(new ProxyConfigurationTab(wizardModel));
            var wrapper = new ProxyWizardModelWrapper(wizardModel);

            if (pollingProxyConfiguration != null)
            {
                var pollingWizardModel = LoadModel(application, pollingProxyConfiguration, selectedInstance);
                wizard.AddTab(new ProxyConfigurationTab(pollingWizardModel));
                wrapper.AddPollingModel(pollingWizardModel);
            }

            wizard.AddTab(new InstallTab(wrapper, container.Resolve<ICommandLineRunner>()) {ReadyMessage = "That's all the information we need. When you click the button below, your proxy settings will be saved and the service will be restarted.", SuccessMessage = "Happy deployments!", ExecuteButtonText = "Apply", Title = "Apply", Header = "Apply"});

            return BuildShell(owner, wizard).ShowDialog();
        }

        ShellView BuildShell(Window owner, TabbedWizard wizard)
        {
            var shellModel = container.Resolve<ShellViewModel>();
            var shell = new ShellView("Proxy Configuration Wizard", shellModel);
            shell.Height = 590;
            shell.Width = 890;
            shell.SetViewContent(wizard);
            shell.Owner = owner;
            return shell;
        }

        static ProxyWizardModel LoadModel(ApplicationName application, IProxyConfiguration proxyConfiguration, string selectedInstance)
        {
            var wizardModel = proxyConfiguration is PollingProxyConfiguration
                ? new PollingProxyWizardModel(selectedInstance, application) { ShowProxySettings = true, ToggleService = false }
                : new ProxyWizardModel(selectedInstance, application) { ShowProxySettings = true, ToggleService = false };

            if (!proxyConfiguration.UseDefaultProxy && string.IsNullOrWhiteSpace(proxyConfiguration.CustomProxyHost))
            {
                wizardModel.UseNoProxy = true;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(proxyConfiguration.CustomProxyHost))
                {
                    wizardModel.UseCustomProxy = true;
                    wizardModel.ProxyPassword = string.Empty;
                    wizardModel.ProxyUsername = proxyConfiguration.CustomProxyUsername;
                    wizardModel.ProxyServerHost = proxyConfiguration.CustomProxyHost;
                    wizardModel.ProxyServerPort = proxyConfiguration.CustomProxyPort;
                }
                else if (!string.IsNullOrWhiteSpace(proxyConfiguration.CustomProxyUsername))
                {
                    wizardModel.UseDefaultProxyCustomCredentials = true;
                    wizardModel.ProxyPassword = string.Empty;
                    wizardModel.ProxyUsername = proxyConfiguration.CustomProxyUsername;
                }
                else
                {
                    wizardModel.UseDefaultProxy = true;
                }
            }
            return wizardModel;
        }
    }
}