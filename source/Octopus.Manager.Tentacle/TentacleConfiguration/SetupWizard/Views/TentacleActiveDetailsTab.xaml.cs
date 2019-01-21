using System;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for TentacleActiveDetailsTab.xaml
    /// </summary>
    public partial class TentacleActiveDetailsTab
    {
        readonly TentacleSetupWizardModel model;

        public TentacleActiveDetailsTab(TentacleSetupWizardModel model)
        {
            this.model = model;
            InitializeComponent();

            DataContext = this.model = model;
        }
    }
}