using System;
using System.Windows;
using Autofac;
using Octopus.Manager.Tentacle.DeleteWizard.Views;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.DeleteWizard
{
    public class DeleteWizardLauncher
    {
        private readonly IComponentContext container;
        private readonly InstanceSelectionModel instanceSelection;

        public DeleteWizardLauncher(IComponentContext container, InstanceSelectionModel instanceSelection)
        {
            this.container = container;
            this.instanceSelection = instanceSelection;
        }

        public bool? ShowDialog(Window owner, ApplicationName application, string selectedInstance)
        {
            var wizardModel = new DeleteWizardModel(application) { InstanceName = instanceSelection.SelectedInstance };

            var wizard = new TabbedWizard();
            wizard.AddTab(new DeleteWelcome(wizardModel));
            wizard.AddTab(new InstallTab(wizardModel, container.Resolve<ICommandLineRunner>()) { ReadyMessage = "When you click the button below, the Windows Service will be stopped and uninstalled, and your instance will be deleted.", SuccessMessage = "Instance deleted", ExecuteButtonText = "DELETE", Title = "Delete", Header = "Delete" });

            var shellModel = container.Resolve<ShellViewModel>();
            var shell = new ShellView("Delete Instance Wizard", shellModel)
            {
                Height = 580,
                Width = 890
            };
            shell.SetViewContent(wizard);
            shell.Owner = owner;
            return shell.ShowDialog();
        }
    }
}