using System;

namespace Octopus.Manager.Tentacle.Proxy
{
    /// <summary>
    /// Interaction logic for ProxyConfigurationTab.xaml
    /// </summary>
    public partial class ProxyConfigurationTab
    {
        public ProxyConfigurationTab(object model)
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