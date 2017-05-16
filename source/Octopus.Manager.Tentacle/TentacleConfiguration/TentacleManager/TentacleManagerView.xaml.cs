using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Octopus.Manager.Core.Shared.DeleteWizard;
using Octopus.Manager.Core.Shared.Dialogs;
using Octopus.Manager.Core.Shared.ProxyWizard;
using Octopus.Manager.Core.Shared.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager
{
    /// <summary>
    /// Interaction logic for TentacleManagerView.xaml
    /// </summary>
    public partial class TentacleManagerView
    {
        readonly InstanceSelectionModel instanceSelection;
        readonly IApplicationInstanceStore instanceStore;
        readonly TentacleSetupWizardLauncher tentacleSetupWizardLauncher;
        readonly ProxyWizardLauncher proxyWizardLauncher;
        readonly DeleteWizardLauncher deleteWizardLaunchers;
        readonly TentacleManagerModel model;

        public TentacleManagerView(TentacleManagerModel model, InstanceSelectionModel instanceSelection, IApplicationInstanceStore instanceStore, TentacleSetupWizardLauncher tentacleSetupWizardLauncher, ProxyWizardLauncher proxyWizardLauncher, DeleteWizardLauncher deleteWizardLaunchers)
        {
            this.instanceSelection = instanceSelection;
            this.instanceStore = instanceStore;
            this.tentacleSetupWizardLauncher = tentacleSetupWizardLauncher;
            this.proxyWizardLauncher = proxyWizardLauncher;
            this.deleteWizardLaunchers = deleteWizardLaunchers;
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
                var defaultInstall = instanceStore.GetInstance(instanceSelection.ApplicationName, instanceSelection.SelectedInstance);
                if (defaultInstall != null && !File.Exists(defaultInstall.ConfigurationFilePath))
                {
                    Log.Octopus().WarnFormat("An instance of {0} named {1} was configured, but the associated configuration file {2} does not exist. Deleting the instance.", defaultInstall.ApplicationName, defaultInstall.InstanceName, defaultInstall.ConfigurationFilePath);
                    instanceStore.DeleteInstance(defaultInstall);
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
            tentacleSetupWizardLauncher.ShowDialog(Window.GetWindow(this), instanceSelection.SelectedInstance);
            instanceSelection.Refresh();
            Refresh();
        }

        void StartServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetStartCommands(), "Starting Tentacle service...");
        }

        void StopServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetStopCommands(), "Stopping Tentacle service...");
            Refresh();
        }

        void RestartServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetRestartCommands(), "Restarting Tentacle service...");
            Refresh();
        }

        void RepairServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetRepairCommands(), "Reinstalling the Tentacle service...");
        }

        void ShowProxy(object sender, EventArgs e)
        {
            proxyWizardLauncher.ShowDialog(Window.GetWindow(this), ApplicationName.Tentacle, instanceSelection.SelectedInstance, model.ProxyConfiguration, model.PollingProxyConfiguration);
            Refresh();
        }

        void DeleteInstance(object sender, EventArgs e)
        {
            deleteWizardLaunchers.ShowDialog(Window.GetWindow(this), ApplicationName.Tentacle, instanceSelection.SelectedInstance);
            instanceSelection.Refresh();
            Refresh();
        }

        void BrowseLogs(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", model.LogsDirectory);
        }

        void BrowseHome(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", model.HomeDirectory);
        }
    }
}