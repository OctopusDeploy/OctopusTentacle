using System;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for TentaclePassiveTab.xaml
    /// </summary>
    public partial class TentaclePassiveTab
    {
        SetupWizardViewModel viewModel;

        public TentaclePassiveTab(SetupWizardViewModel viewModel)
        {
            InitializeComponent();

            DataContext = this.viewModel = viewModel;

            // Default to true only when using the UI
            viewModel.FirewallException = true;
        }
    }
}