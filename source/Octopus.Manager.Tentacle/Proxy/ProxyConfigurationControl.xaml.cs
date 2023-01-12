using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Octopus.Manager.Tentacle.Proxy
{
    /// <summary>
    /// Interaction logic for ProxyConfigurationControl.xaml
    /// </summary>
    public partial class ProxyConfigurationControl : UserControl
    {
        public bool ShowHeader
        {
            get => (bool)GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }

        public static readonly DependencyProperty ShowHeaderProperty = DependencyProperty.Register(
            "ShowHeader", typeof(bool), typeof(ProxyConfigurationControl), new PropertyMetadata(true));

        public ProxyConfigurationControl()
        {
            InitializeComponent();
        }
    }
}
