using System;
using System.Windows;
using Octopus.Diagnostics;

namespace Octopus.Manager.Tentacle.Dialogs
{
    /// <summary>
    /// Interaction logic for ErrorDialog.xaml
    /// </summary>
    public partial class ErrorDialog : Window
    {
        private ErrorDialog()
        {
            InitializeComponent();
        }

        public static void ShowDialog(string title, Exception ex, bool details = false)
        {
            var errorAsString = ex.PrettyPrint();
            var errorSummary = ex.PrettyPrint(false);
            if (errorAsString.Contains("System.Core, Version=2.0.5.0"))
            {
                errorSummary = "You are missing a required Microsoft .NET Framework patch on this server";
                errorAsString = "The patch enables portable class libraries to be used with the .NET Framework 4.0."
                    + Environment.NewLine + Environment.NewLine
                    + "You can download the patch from: http://support.microsoft.com/kb/2468871"
                    + Environment.NewLine + Environment.NewLine + errorAsString;
            }

            var window = new ErrorDialog
            {
                ErrorTextBox = { Text = errorAsString },
                errorSummary = { Text = errorSummary },
                title = { Text = title },
                Title = title
            };

            if (!details)
            {
                window.ErrorTextBox.Visibility = Visibility.Collapsed;
                window.Height = 230;
            }

            window.ShowDialog();
        }

        public static void ShowDialog(Exception ex)
        {
            ShowDialog("Unhandled error", ex, true);
        }
    }
}