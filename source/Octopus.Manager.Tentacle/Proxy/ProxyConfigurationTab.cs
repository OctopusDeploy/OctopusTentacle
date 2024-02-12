namespace Octopus.Manager.Tentacle.Proxy
{
    /// <summary>
    /// Interaction logic for ProxyConfigurationTab.xaml
    /// </summary>
    public partial class ProxyConfigurationTab
    {
        public ProxyConfigurationTab(ProxyWizardModel model)
        {
            InitializeComponent();
            DataContext = model;
        }

        public ProxyConfigurationTab()
        {
            InitializeComponent();
        }
    }
}