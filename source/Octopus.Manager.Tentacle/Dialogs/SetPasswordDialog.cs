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
#pragma warning disable CA1416
            DialogHost.CloseDialogCommand.Execute(true, null);
#pragma warning restore CA1416
        }
    }
}