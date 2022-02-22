using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Octopus.Manager.Tentacle.Converters
{
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parameterString = parameter as string;
            if (parameterString == null || value == null)
                return Visibility.Collapsed;

            if (Enum.IsDefined(value.GetType(), value) == false)
                return Visibility.Collapsed;

            var parameterValue = Enum.Parse(value.GetType(), parameterString);

            return parameterValue.Equals(value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}