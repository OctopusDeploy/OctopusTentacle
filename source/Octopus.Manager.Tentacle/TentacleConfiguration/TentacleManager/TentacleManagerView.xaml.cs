using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager
{
    /// <summary>
    /// Interaction logic for TentacleManagerView.xaml
    /// </summary>
    public partial class TentacleManagerView
    {
        private readonly InstanceSelectionModel instanceSelection;
        private readonly IApplicationInstanceManager instanceManager;
        private readonly IApplicationInstanceStore instanceStore;
        private readonly TentacleSetupWizardLauncher tentacleSetupWizardLauncher;
        private readonly ProxyWizardLauncher proxyWizardLauncher;
        private readonly DeleteWizardLauncher deleteWizardLaunchers;
        private readonly TentacleManagerModel model;

        public TentacleManagerView(TentacleManagerModel model,
            InstanceSelectionModel instanceSelection,
            IApplicationInstanceManager instanceManager,
            IApplicationInstanceStore instanceStore,
            TentacleSetupWizardLauncher tentacleSetupWizardLauncher,
            ProxyWizardLauncher proxyWizardLauncher,
            DeleteWizardLauncher deleteWizardLaunchers)
        {
            this.instanceSelection = instanceSelection;
            this.instanceManager = instanceManager;
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

        private void ViewLoaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Refresh()
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

        private void SetupTentacle(object sender, RoutedEventArgs e)
        {
            tentacleSetupWizardLauncher.ShowDialog(Window.GetWindow(this), instanceSelection.SelectedInstance);
            instanceSelection.Refresh();
            Refresh();
        }

        private void StartServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetStartCommands(), "Starting Tentacle service...", model.LogsDirectory);
        }

        private void StopServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetStopCommands(), "Stopping Tentacle service...", model.LogsDirectory);
            Refresh();
        }

        private void RestartServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetRestartCommands(), "Restarting Tentacle service...", model.LogsDirectory);
            Refresh();
        }

        private void RepairServiceClicked(object sender, EventArgs e)
        {
            RunProcessDialog.ShowDialog(Window.GetWindow(this), model.ServiceWatcher.GetRepairCommands(), "Reinstalling the Tentacle service...", model.LogsDirectory);
        }

        private void ShowProxy(object sender, EventArgs e)
        {
            proxyWizardLauncher.ShowDialog(Window.GetWindow(this), ApplicationName.Tentacle, instanceSelection.SelectedInstance, model.ProxyConfiguration, model.PollingProxyConfiguration);
            Refresh();
        }

        private void DeleteInstance(object sender, EventArgs e)
        {
            deleteWizardLaunchers.ShowDialog(Window.GetWindow(this), ApplicationName.Tentacle, instanceSelection.SelectedInstance);
            instanceSelection.Refresh();
            Refresh();
        }

        private void BrowseLogs(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", model.LogsDirectory);
        }

        private void BrowseHome(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", model.HomeDirectory);
        }

        private async void CreateNewInstance(object sender, RoutedEventArgs e)
        {
            var result = await DialogHost.Show(new NewInstanceNameDialog(instanceSelection.Instances.Select(q => q.InstanceName)), Window.GetWindow(this)?.Title);

            if (result is string typedResult) instanceSelection.New(typedResult);
        }

        private void CopyThumbprintToClipboard(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(model.Thumbprint);
        }
    }

    public class MultiStatusToColorValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Format("{0} {1}", values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}