using System.Windows;
using Autofac;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard
{
    public class TentacleSetupWizardLauncher
    {
        readonly IComponentContext container;
        readonly InstanceSelectionModel instanceSelection;

        public TentacleSetupWizardLauncher(IComponentContext container, InstanceSelectionModel instanceSelection)
        {
            this.container = container;
            this.instanceSelection = instanceSelection;
        }

        public bool? ShowDialog(Window owner, string selectedInstance)
        {
            var wizardModel = new TentacleSetupWizardModel(selectedInstance, instanceSelection.ApplicationName, new PollingProxyWizardModel(selectedInstance, instanceSelection.ApplicationName));
            var wizard = new TabbedWizard();
            wizard.AddTab(new CommunicationStyleTab(wizardModel));
            wizard.AddTab(new StorageTab(wizardModel));
            wizard.AddTab(new ProxyConfigurationTab(wizardModel.ProxyWizardModel));
            wizard.AddTab(new OctopusServerConnectionTab(wizardModel));
            wizard.AddTab(new Views.MachineType(wizardModel));
            wizard.AddTab(new TentacleActiveDetailsTab(wizardModel));
            wizard.AddTab(new TentaclePassiveTab(wizardModel));
            wizard.AddTab(new ReviewAndRunScriptTabView(new ReviewAndRunScriptTabViewModel(wizardModel, container.Resolve<ICommandLineRunner>())) {ReadyMessage = "You're ready to install an Octopus Tentacle.", SuccessMessage = "Installation complete!"});

            var shellModel = container.Resolve<ShellViewModel>();
            var shell = new ShellView("Tentacle Setup Wizard", shellModel)
            {
                Height = 650,
                Width = 890
            };
            shell.SetViewContent(wizard);
            shell.Owner = owner;
            return shell.ShowDialog();
        }
    }
}