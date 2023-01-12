using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Octopus.Manager.Tentacle.Properties;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Controls
{
    
    /// <summary>
    /// Interaction logic for ErrorPanelControl.xaml
    /// </summary>
    public partial class ErrorPanelControl : UserControl, INotifyPropertyChanged
    {
        public ErrorPanelControl()
        {
            InitializeComponent();
        }

        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(
            "ErrorMessage", typeof(string), typeof(ErrorPanelControl), new PropertyMetadata(string.Empty));

        public string ErrorMessageHeader
        {
            get => (string)GetValue(ErrorMessageHeaderProperty);
            set => SetValue(ErrorMessageHeaderProperty, value);
        }

        public static readonly DependencyProperty ErrorMessageHeaderProperty = DependencyProperty.Register(
            "ErrorMessageHeader", typeof(string), typeof(ErrorPanelControl), new PropertyMetadata(string.Empty));

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
