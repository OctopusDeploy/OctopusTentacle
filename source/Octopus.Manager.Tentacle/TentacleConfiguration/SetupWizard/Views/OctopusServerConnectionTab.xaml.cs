﻿using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using MaterialDesignThemes.Wpf;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for OctopusServerConnectionTab.xaml
    /// </summary>
    public partial class OctopusServerConnectionTab
    {
        readonly TentacleSetupWizardModel model;

        public OctopusServerConnectionTab(TentacleSetupWizardModel model)
        {
            InitializeComponent();
            DataContext = this.model = model;
        }

        void Navigate(object sender, RequestNavigateEventArgs e)
        {
            BrowserHelper.Open(e.Uri);
            e.Handled = true;
        }

        public override async Task OnNext(CancelEventArgs e)
        {
            await base.OnNext(e);
            model.PushRuleSet("TentacleActive");
            model.Validate();
            if (!model.IsValid || !model.ProxyWizardModel.IsValid)
            {
                e.Cancel = true;
                return;
            }

            await ShowConnectionDialog();
            if (!model.HaveCredentialsBeenVerified)
            {
                e.Cancel = true;
            }
        }

        async Task ShowConnectionDialog()
        {
            await DialogHost.Show(new ServerConnectionDialog(model) { DataContext = model }, "Tentacle Setup Wizard");
        }

        async void ProxyButton_OnClick(object sender, RoutedEventArgs e)
        {
            await DialogHost.Show(new ProxyConfigurationDialog() {DataContext = model.ProxyWizardModel}, "Tentacle Setup Wizard");
        }
    }
}