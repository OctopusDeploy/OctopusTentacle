using System;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for TentaclePassiveTab.xaml
    /// </summary>
    public partial class TentaclePassiveTab
    {
        private TentacleSetupWizardModel model;

        public TentaclePassiveTab(TentacleSetupWizardModel model)
        {
            InitializeComponent();

            DataContext = this.model = model;

            // Default to true only when using the UI
            model.FirewallException = true;
        }
    }
}