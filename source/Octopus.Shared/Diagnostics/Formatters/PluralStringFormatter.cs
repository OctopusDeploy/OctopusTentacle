using System;
using Octopus.Shared.Util;

namespace Octopus.Shared.Diagnostics.Formatters
{
    public class PluralStringFormatter : IFormatProvider, ICustomFormatter
    {
        public object? GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : null;
        }

        public string? Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (!(arg is int) || string.IsNullOrWhiteSpace(format))
            {
                return null;
            }

            var formatParts = format.Split(':');
            if (formatParts.Length != 2)
            {
                return null;
            }

            if (!string.Equals(formatParts[0], "p", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var count = (int) arg;
            return formatParts[1].Plural(count);
        }
    }
}