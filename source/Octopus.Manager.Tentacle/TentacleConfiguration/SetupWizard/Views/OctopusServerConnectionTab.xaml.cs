using System.ComponentModel;
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
        readonly SetupTentacleWizardModel viewModel;

        public OctopusServerConnectionTab(SetupTentacleWizardModel viewModel)
        {
            InitializeComponent();
            DataContext = this.viewModel = viewModel;
        }

        void Navigate(object sender, RequestNavigateEventArgs e)
        {
            BrowserHelper.Open(e.Uri);
            e.Handled = true;
        }

        public override async Task OnNext(CancelEventArgs e)
        {
            await base.OnNext(e);
            viewModel.PushRuleSet("TentacleActive");
            viewModel.Validate();
            if (!viewModel.IsValid || !viewModel.ProxyWizardModel.IsValid)
            {
                e.Cancel = true;
                return;
            }

            await ShowConnectionDialog();
            if (!viewModel.HaveCredentialsBeenVerified)
            {
                e.Cancel = true;
            }
        }

        async Task ShowConnectionDialog()
        {
            await DialogHost.Show(new ServerConnectionDialog(viewModel) { DataContext = viewModel }, "Tentacle Setup Wizard");
        }
    }
}