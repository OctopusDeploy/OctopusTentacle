namespace Octopus.Manager.Tentacle.DeleteWizard.Views
{
    /// <summary>
    /// Interaction logic for DeleteWelcome.xaml
    /// </summary>
    public partial class DeleteWelcome
    {
        public DeleteWelcome(DeleteWizardModel model)
        {
            InitializeComponent();
            DataContext = model;
        }
    }
}
