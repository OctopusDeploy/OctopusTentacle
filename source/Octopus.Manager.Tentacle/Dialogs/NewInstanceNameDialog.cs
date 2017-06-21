using System;
using System.Collections.Generic;
using System.Windows;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.Dialogs
{
    public partial class NewInstanceNameDialog : Window
    {
        readonly HashSet<string> inUse = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        public NewInstanceNameDialog(IEnumerable<string> existing)
        {
            InitializeComponent();

            inUse.AddRange(existing);

            instanceNameBox.Focus();
        }

        public string InstanceName
        {
            get { return instanceNameBox.Text; }
        }

        void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InstanceName))
            {
                MessageBox.Show(this, "Please enter an instance name.", "Instance Name", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(InstanceName))
            {
                MessageBox.Show(this, "An instance with this name already exists. Please enter a new instance name.", "Instance Name", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}