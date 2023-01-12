using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Octopus.Manager.Tentacle.Controls
{
    public class ErrorMessage : Control
    {
        bool isAttached;

        static ErrorMessage()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof (ErrorMessage), new FrameworkPropertyMetadata(typeof (ErrorMessage)));
        }

        public ErrorMessage()
        {
            Loaded += (sender, args) => Refresh();
            Unloaded += (sender, args) => DetachFromPropertyChanged();
        }

        public bool HasError
        {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }

        public string Error
        {
            get => (string)GetValue(ErrorProperty);
            set => SetValue(ErrorProperty, value);
        }

        public object ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public string ErrorPath
        {
            get => (string)GetValue(ErrorPathProperty);
            set => SetValue(ErrorPathProperty, value);
        }

        void DetachFromPropertyChanged()
        {
            var inpc = ViewModel as INotifyPropertyChanged;
            if (inpc != null)
            {
                inpc.PropertyChanged -= OnModelPropertyChanged;
                isAttached = false;
            }
        }

        void AttachToPropertyChanged()
        {
            var inpc = ViewModel as INotifyPropertyChanged;
            if (inpc != null)
            {
                inpc.PropertyChanged -= OnModelPropertyChanged;
                inpc.PropertyChanged += OnModelPropertyChanged;
                isAttached = true;
            }
        }

        void OnModelPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            Refresh();
        }

        void Refresh()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(Refresh));
                return;
            }

            if (IsLoaded && !isAttached)
            {
                AttachToPropertyChanged();
            }

            if (!string.IsNullOrWhiteSpace(ErrorPath))
            {
                var dataError = ViewModel as IDataErrorInfo;
                if (dataError != null)
                {
                    Error = dataError[ErrorPath];
                    Trace.WriteLine("Error for: " + ErrorPath + ": " + Error);
                }
            }
        }

        static void ErrorPathSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ErrorMessage)d).Refresh();
        }

        static void ViewModelSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ErrorMessage)d).Refresh();
        }

        static void ErrorPropertySet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ErrorMessage) d;
            control.HasError = !string.IsNullOrWhiteSpace(control.Error);
        }

        public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register("Error", typeof (string), typeof (ErrorMessage), new PropertyMetadata(null, ErrorPropertySet));
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof (object), typeof (ErrorMessage), new PropertyMetadata(null, ViewModelSet));

        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.Register("HasError", typeof (bool), typeof (ErrorMessage), new PropertyMetadata(false));

        public static readonly DependencyProperty ErrorPathProperty =
            DependencyProperty.Register("ErrorPath", typeof (string), typeof (ErrorMessage), new PropertyMetadata(null, ErrorPathSet));
    }
}