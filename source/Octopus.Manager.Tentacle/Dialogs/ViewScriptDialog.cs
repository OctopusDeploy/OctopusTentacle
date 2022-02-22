using System;
using System.Windows;

namespace Octopus.Manager.Tentacle.Dialogs
{
    /// <summary>
    /// Interaction logic for ViewScriptDialog.xaml
    /// </summary>
    public partial class ViewScriptDialog : Window
    {
        private ViewScriptDialog()
        {
            InitializeComponent();
        }

        public static void ShowDialog(Window owner, string code)
        {
            var window = new ViewScriptDialog
            {
                Owner = owner,
                ScriptTextBox = { Text = code }
            };

            window.ShowDialog();
        }

        private void CopyToClipboardButton(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ScriptTextBox.Text);
        }
    }
}