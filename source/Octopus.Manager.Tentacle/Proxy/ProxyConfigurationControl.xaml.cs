using System;
using System.Windows;
using System.Windows.Controls;

namespace Octopus.Manager.Tentacle.Proxy
{
    /// <summary>
    /// Interaction logic for ProxyConfigurationControl.xaml
    /// </summary>
    public partial class ProxyConfigurationControl : UserControl
    {
        public static readonly DependencyProperty ShowHeaderProperty = DependencyProperty.Register(
            "ShowHeader", typeof(bool), typeof(ProxyConfigurationControl), new PropertyMetadata(true));

        public ProxyConfigurationControl()
        {
            InitializeComponent();
        }

        public bool ShowHeader
        {
            get => (bool)GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }
    }
}