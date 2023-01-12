using System.Windows;

namespace Octopus.Manager.Tentacle.Dialogs
{
    /// <summary>
    /// Interaction logic for ViewScriptDialog.xaml
    /// </summary>
    public partial class ViewScriptDialog : Window
    {
        ViewScriptDialog()
        {
            InitializeComponent();
        }

        public static void ShowDialog(Window owner, string code)
        {
            var window = new ViewScriptDialog
            {
                Owner = owner,
                ScriptTextBox = {Text = code}
            };

            window.ShowDialog();
        }

        void CopyToClipboardButton(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ScriptTextBox.Text);

        }
    }
}