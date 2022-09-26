using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Octopus.Manager.Tentacle.Validators
{
    public class InstanceName : ValidationRule
    {
        public InstanceNameWrapper InstanceNameWrapper { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (!(value is string typedValue)) return new ValidationResult(false, "Invalid string.");

            if (string.IsNullOrWhiteSpace(typedValue)) return new ValidationResult(false, "Instance name is required.");

            if (InstanceNameWrapper.ExistingInstanceNames.Contains(typedValue)) return new ValidationResult(false, "An instance with this name already exists.");

            return ValidationResult.ValidResult;
        }
    }

    public class InstanceNameWrapper : DependencyObject
    {
        public static readonly DependencyProperty ExistingInstanceNamesProperty = DependencyProperty.Register(
            "ExistingInstanceNames", typeof(HashSet<string>), typeof(InstanceNameWrapper), new PropertyMetadata(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        public HashSet<string> ExistingInstanceNames
        {
            get => (HashSet<string>)GetValue(ExistingInstanceNamesProperty);
            set => SetValue(ExistingInstanceNamesProperty, value);
        }
    }
}