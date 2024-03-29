using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.DeleteWizard.Views;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using MachineType = Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views.MachineType;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager
{
    /// <summary>
    /// Interaction logic for TentacleManagerView.xaml
    /// </summary>
    public partial class TentacleManagerView
    {
        readonly InstanceSelectionModel instanceSelection;
        readonly IApplicationInstanceManager instanceManager;
        readonly IApplicationInstanceStore instanceStore;
        readonly TentacleManagerModel model;

        public TentacleManagerView(
            TentacleManagerModel model,
            InstanceSelectionModel instanceSelection,
            IApplicationInstanceManager instanceManager,
            IApplicationInstanceStore instanceStore)
        {
            this.instanceSelection = instanceSelection;
            this.instanceManager = instanceManager;
            this.instanceStore = instanceStore;
            InitializeComponent();
            findingInstallation.Visibility = Visibility.Visible;
            newInstallation.Visibility = Visibility.Collapsed;
            existingInstallation.Visibility = Visibility.Collapsed;

            instanceSelection.SelectionChanged += Refresh;

            DataContext = this.model = model;
            Loaded += ViewLoaded;
        }

        void ViewLoaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        void Refresh()
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                var instances = instanceStore.ListInstances();
                var defaultInstall = instances.SingleOrDefault(s => s.InstanceName == instanceSelection.SelectedInstance);
                if (defaultInstall != null && !File.Exists(defaultInstall.ConfigurationFilePath))
                {
                    new SystemLog().WarnFormat("An instance of {0} named {1} was configured, but the associated configuration file {2} does not exist. Deleting the instance.", ApplicationName.Tentacle, defaultInstall.InstanceName, defaultInstall.ConfigurationFilePath);
                    instanceManager.DeleteInstance(defaultInstall.InstanceName);
                    defaultInstall = null;
                }

                findingInstallation.Visibility = Visibility.Collapsed;
                newInstallation.Visibility = Visibility.Collapsed;
                existingInstallation.Visibility = Visibility.Collapsed;
                if (defaultInstall == null)
                {
                    newInstallation.Visibility = Visibility.Visible;
                }
                else
                {
                    existingInstallation.Visibility = Visibility.Visible;
                    model.Load(defaultInstall);
                }
            }));
        }

        void SetupTentacle(object sender, RoutedEventArgs e)
        {
            var setupTentacleWizardModel = model.CreateSetupTentacleWizardModel();
            var setupTentacleWizardView = CreateSetUpTentacleWizardView(setupTentacleWizardModel);
            setupTentacleWizardView.ShowDialog();
            
            instanceSelection.Refresh();
            Refresh();
        }

        Window CreateSetUpTentacleWizardView(SetupTentacleWizardModel setupTentacleWizardModel)
        {
            var wizard = new TabbedWizard();
            wizard.AddTab(new CommunicationStyleTab(setupTentacleWizardModel));
            wizard.AddTab(new StorageTab(setupTentacleWizardModel));
            wizard.AddTab(new ProxyConfigurationTab(setupTentacleWizardModel.ProxyWizardModel));
            wizard.AddTab(new OctopusServerConnectionTab(setupTentacleWizardModel));
            wizard.AddTab(new MachineType(setupTentacleWizardModel));
            wizard.AddTab(new TentacleActiveDetailsTab(setupTentacleWizardModel));
            wizard.AddTab(new TentaclePassiveTab(setupTentacleWizardModel));
            wizard.AddTab(new ReviewAndRunScriptTabView(setupTentacleWizardModel.ReviewAndRunScriptTabViewModel)
            {
                ReadyMessage = "You're ready to install an Octopus Tentacle.",
                SuccessMessage = "Installation complete!"
            });

            var shell = new ShellView("Tentacle Setup Wizard", setupTentacleWizardModel)
            {
                Height = 650,
                Width = 890
            };
            shell.SetViewContent(wizard);
            shell.Owner = Window.GetWindow(this);
            return shell;
        }

        void StartServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetStartCommands(), "Starting Tentacle service...", model.LogsDirectory);
        }

        void StopServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetStopCommands(), "Stopping Tentacle service...", model.LogsDirectory);
            Refresh();
        }

        void RestartServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetRestartCommands(), "Restarting Tentacle service...", model.LogsDirectory);
            Refresh();
        }

        void RepairServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetRepairCommands(), "Reinstalling the Tentacle service...", model.LogsDirectory);
        }

        void ShowProxy(object sender, EventArgs e)
        {
            var proxyWizardModelWrapper = model.CreateProxyWizardModelWrapper();
            var proxyWizardView = CreateProxyWizardView(proxyWizardModelWrapper);
            proxyWizardView.ShowDialog();
            
            instanceSelection.Refresh();
            Refresh();
        }

        Window CreateProxyWizardView(ProxyWizardModelWrapper proxyWizardModelWrapper)
        {
            var wizard = new TabbedWizard();
            
            wizard.AddTab(new ProxyConfigurationTab(proxyWizardModelWrapper.ProxyWizardModel));

            if (proxyWizardModelWrapper.PollingProxyWizardModel != null)
            {
                wizard.AddTab(new ProxyConfigurationTab(proxyWizardModelWrapper.PollingProxyWizardModel));
            }

            wizard.AddTab(
                new ReviewAndRunScriptTabView(new ReviewAndRunScriptTabViewModel(proxyWizardModelWrapper, model.CommandLineRunner))
                {
                    ReadyMessage = "That's all the information we need. When you click the button below, your proxy settings will be saved and the service will be restarted.",
                    SuccessMessage = "Happy deployments!",
                    ExecuteButtonText = "APPLY",
                    Title = "Apply",
                    Header = "Apply"
                }
            );
            
            var shell = new ShellView("Proxy Configuration Wizard", proxyWizardModelWrapper)
            {
                Height = 590,
                Width = 890
            };
            shell.SetViewContent(wizard);
            shell.Owner = Window.GetWindow(this);
            return shell;
        }

        void DeleteInstance(object sender, EventArgs e)
        {
            var deleteWizardViewModel = model.StartDeleteWizard();
            var deleteWizardView = CreateDeleteWizardView(deleteWizardViewModel);
            deleteWizardView.ShowDialog();
            
            instanceSelection.Refresh();
            Refresh();
        }

        Window CreateDeleteWizardView(DeleteWizardModel deleteWizardViewModel)
        {
            var wizard = new TabbedWizard();
            wizard.AddTab(new DeleteWelcome(deleteWizardViewModel));
            wizard.AddTab(
                new ReviewAndRunScriptTabView(new ReviewAndRunScriptTabViewModel(deleteWizardViewModel, model.CommandLineRunner))
                {
                    // TODO: These read-only properties should probably be on the DeleteWizardViewModel 
                    ReadyMessage = "When you click the button below, the Windows Service will be stopped and uninstalled, and your instance will be deleted.",
                    SuccessMessage = "Instance deleted",
                    ExecuteButtonText = "DELETE",
                    Title = "Delete",
                    Header = "Delete"
                }
            );

            var shell = new ShellView("Delete Instance Wizard", deleteWizardViewModel)
            {
                Height = 580,
                Width = 890
            };
            shell.SetViewContent(wizard);
            shell.Owner = Window.GetWindow(this);

            return shell;
        }

        void BrowseLogs(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", model.LogsDirectory);
        }

        void BrowseHome(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", model.HomeDirectory);
        }

        async void CreateNewInstance(object sender, RoutedEventArgs e)
        {
#pragma warning disable CA1416
            var result = await DialogHost.Show(new NewInstanceNameDialog(instanceSelection.Instances.Select(q => q.InstanceName)), Window.GetWindow(this)?.Title);
#pragma warning restore CA1416

            if (result is string typedResult)
            {
                instanceSelection.New(typedResult);
            }
        }

        void CopyThumbprintToClipboard(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(model.Thumbprint);
        }
    }

    public class MultiStatusToColorValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return String.Format("{0} {1}", values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
