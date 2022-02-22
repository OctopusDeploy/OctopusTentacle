using System;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for WelcomeTab.xaml
    /// </summary>
    public partial class WelcomeTab
    {
        public WelcomeTab(object model)
        {
            InitializeComponent();

            DataContext = model;
        }
    }
}