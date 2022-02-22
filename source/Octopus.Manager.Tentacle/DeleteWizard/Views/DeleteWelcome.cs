using System;

namespace Octopus.Manager.Tentacle.DeleteWizard.Views
{
    /// <summary>
    /// Interaction logic for DeleteWelcome.xaml
    /// </summary>
    public partial class DeleteWelcome
    {
        public DeleteWelcome(object model)
        {
            InitializeComponent();

            DataContext = model;
        }
    }
}