using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for TentacleActiveDetailsTab.xaml
    /// </summary>
    public partial class TentacleActiveDetailsTab
    {
        private readonly TentacleSetupWizardModel model;

        public TentacleActiveDetailsTab(TentacleSetupWizardModel model)
        {
            this.model = model;
            InitializeComponent();

            DataContext = this.model = model;
        }

        public override async Task OnNext(CancelEventArgs e)
        {
            await base.OnNext(e);
            model.SkipServerRegistration = false;
        }

        public override async Task OnSkip(CancelEventArgs e)
        {
            await base.OnSkip(e);
            model.SkipServerRegistration = true;
        }

        private async void RefreshClicked(object sender, RoutedEventArgs e)
        {
            await model.RefreshSpaceData();
        }
    }
}