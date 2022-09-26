using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Octopus.Client.Model;

namespace Octopus.Manager.Tentacle.Dialogs
{
    public partial class EditTrustedOctopusDialog
    {
        public EditTrustedOctopusDialog()
        {
            InitializeComponent();

            thumbprintText.Focus();
        }

        public string Thumbprint
        {
            get => thumbprintText.Text;
            set => thumbprintText.Text = value;
        }

        public CommunicationStyle CommunicationStyle
        {
            get => ((ComboBoxItem)style.SelectedItem).Tag.Equals("Poll") ? CommunicationStyle.TentacleActive : CommunicationStyle.TentaclePassive;
            set => style.SelectedIndex = value == CommunicationStyle.TentacleActive ? 1 : 0;
        }

        public string ServerAddress
        {
            get => address.Text;
            set => address.Text = value;
        }

        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            thumbprintText.Text = new string((thumbprintText.Text ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

            if (!string.IsNullOrWhiteSpace(thumbprintText.Text))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(this, "Please enter a thumbprint value.", "Octopus", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Style_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            address.Visibility = addressLabel.Visibility = CommunicationStyle == CommunicationStyle.TentacleActive
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}