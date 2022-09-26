using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Octopus.CoreUtilities.Extensions;

namespace Octopus.Manager.Tentacle.Dialogs
{
    public partial class NewInstanceNameDialog : UserControl
    {
        public static readonly DependencyProperty InstanceNameProperty = DependencyProperty.Register(
            "InstanceName", typeof(string), typeof(NewInstanceNameDialog), new PropertyMetadata(string.Empty));

        public NewInstanceNameDialog(IEnumerable<string> existing)
        {
            ExistingInstanceNames = new HashSet<string>();
            ExistingInstanceNames.AddRange(existing);

            InitializeComponent();
        }

        public string InstanceName
        {
            get => (string)GetValue(InstanceNameProperty);
            set => SetValue(InstanceNameProperty, value);
        }

        public HashSet<string> ExistingInstanceNames { get; set; }
    }
}