using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Octopus.Manager.Tentacle.Dialogs;

namespace Octopus.Manager.Tentacle.Controls
{
    public class PasswordEditor : Control
    {
        static PasswordEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof (PasswordEditor), new FrameworkPropertyMetadata(typeof (PasswordEditor)));
        }

        public PasswordEditor()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.New, ChangePasswordClicked));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, ChangePasswordClicked));
        }

        public string Password
        {
            get => (string)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        public string DisplayPassword
        {
            get => (string)GetValue(DisplayPasswordProperty);
            set => SetValue(DisplayPasswordProperty, value);
        }

        static void PasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (PasswordEditor)d;
            editor.DisplayPassword = string.IsNullOrWhiteSpace(editor.Password) ? "Set password" : "Change password";
        }

        async void ChangePasswordClicked(object sender, ExecutedRoutedEventArgs e)
        {
#pragma warning disable CA1416
            var dialogHost = FindParent<DialogHost>(this);
#pragma warning restore CA1416
            if (dialogHost == null) throw new Exception("Cannot find a parent DialogHost control.");

            var setPasswordDialog = new SetPasswordDialog();
#pragma warning disable CA1416
            var result = await DialogHost.Show(setPasswordDialog, dialogHost.Identifier);
#pragma warning restore CA1416
            if (!(result is bool typedResult)) return;
            if (typedResult)
            {
                Password = setPasswordDialog.Password;
            }
        }

        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            var parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            if (parentObject is T parent)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register("Password", typeof (string), typeof (PasswordEditor), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PasswordChanged));
        public static readonly DependencyProperty DisplayPasswordProperty = DependencyProperty.Register("DisplayPassword", typeof (string), typeof (PasswordEditor), new PropertyMetadata("Set password"));
    }
}
