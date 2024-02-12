﻿using System;
using System.Windows.Navigation;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard.Views
{
    /// <summary>
    /// Interaction logic for MachineType.xaml
    /// </summary>
    public partial class MachineType
    {
        readonly SetupTentacleWizardModel viewModel;

        public MachineType(SetupTentacleWizardModel viewModel)
        {
            InitializeComponent();

            DataContext = this.viewModel = viewModel;
        }

        void Navigate(object sender, RequestNavigateEventArgs e)
        {
            BrowserHelper.Open(e.Uri);
            e.Handled = true;
        }
    }
}
