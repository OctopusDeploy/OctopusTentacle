using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace Octopus.Manager.Tentacle.Dialogs
{
    public partial class SetPasswordDialog : UserControl
    {
        public SetPasswordDialog()
        {
            InitializeComponent();
        }

        public string Password => password.Password;

        void SaveClicked(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(true, null);
        }
    }
}