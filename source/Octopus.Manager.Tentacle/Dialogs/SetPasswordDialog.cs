using System.Windows;

namespace Octopus.Manager.Tentacle.Dialogs
{
    public partial class SetPasswordDialog : Window
    {
        public SetPasswordDialog()
        {
            InitializeComponent();
        }

        public string Password => password.Password;

        void SaveClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}