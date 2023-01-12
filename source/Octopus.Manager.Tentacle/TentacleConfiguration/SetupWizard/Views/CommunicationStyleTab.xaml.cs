using System.Windows.Navigation;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for CommunicationStyleTab.xaml
    /// </summary>
    public partial class CommunicationStyleTab
    {
        readonly TentacleSetupWizardModel model;

        public CommunicationStyleTab(TentacleSetupWizardModel model)
        {
            InitializeComponent();

            DataContext = this.model = model;
        }

        void Navigate(object sender, RequestNavigateEventArgs e)
        {
            BrowserHelper.Open(e.Uri);
            e.Handled = true;
        }
    }
}