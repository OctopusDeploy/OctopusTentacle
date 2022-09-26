using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Octopus.Manager.Tentacle.Properties;

namespace Octopus.Manager.Tentacle.Controls
{
    /// <summary>
    /// Interaction logic for ErrorPanelControl.xaml
    /// </summary>
    public partial class ErrorPanelControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(
            "ErrorMessage", typeof(string), typeof(ErrorPanelControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ErrorMessageHeaderProperty = DependencyProperty.Register(
            "ErrorMessageHeader", typeof(string), typeof(ErrorPanelControl), new PropertyMetadata(string.Empty));

        public ErrorPanelControl()
        {
            InitializeComponent();
        }

        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public string ErrorMessageHeader
        {
            get => (string)GetValue(ErrorMessageHeaderProperty);
            set => SetValue(ErrorMessageHeaderProperty, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}