﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        void ChangePasswordClicked(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new SetPasswordDialog();
            dialog.Owner = Window.GetWindow(this);
            var result = dialog.ShowDialog();
            if (result ?? false)
            {
                Password = dialog.Password;
            }
        }

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register("Password", typeof (string), typeof (PasswordEditor), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PasswordChanged));
        public static readonly DependencyProperty DisplayPasswordProperty = DependencyProperty.Register("DisplayPassword", typeof (string), typeof (PasswordEditor), new PropertyMetadata("Set password"));
    }
}