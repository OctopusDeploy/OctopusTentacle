using System;
using System.Globalization;
using System.Windows.Data;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Converters
{
    public class CapitalizeFirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typedValue)
            {
                return typedValue.FirstCharToUpper();
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
