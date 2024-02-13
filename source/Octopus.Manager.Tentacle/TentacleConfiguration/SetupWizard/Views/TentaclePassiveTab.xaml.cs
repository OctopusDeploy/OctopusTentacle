using System;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for TentaclePassiveTab.xaml
    /// </summary>
    public partial class TentaclePassiveTab
    {
        public TentaclePassiveTab(SetupTentacleWizardModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;

            // Default to true only when using the UI
            viewModel.FirewallException = true;
        }
    }
}