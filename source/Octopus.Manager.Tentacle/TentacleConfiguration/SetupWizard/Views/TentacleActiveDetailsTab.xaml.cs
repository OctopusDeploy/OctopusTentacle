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
        readonly SetupTentacleWizardModel viewModel;

        public TentacleActiveDetailsTab(SetupTentacleWizardModel viewModel)
        {
            this.viewModel = viewModel;
            InitializeComponent();

            DataContext = this.viewModel = viewModel;
        }

        public override async Task OnNext(CancelEventArgs e)
        {
            await base.OnNext(e);
            viewModel.SkipServerRegistration = false;
        }

        public override async Task OnSkip(CancelEventArgs e)
        {
            await base.OnSkip(e);
            viewModel.SkipServerRegistration = true;
        }

        async void RefreshClicked(object sender, RoutedEventArgs e)
        {
            await viewModel.RefreshSpaceData();
        }
    }
}