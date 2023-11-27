using System;
using System.Diagnostics.CodeAnalysis;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Diagnostics.Formatters
{
    public class PluralStringFormatter : IFormatProvider, ICustomFormatter
    {
        [return: NotNullIfNotNull("formatType")]
        public object? GetFormat(Type? formatType)
            => formatType == typeof(ICustomFormatter) ? this : null;

#pragma warning disable 8766 // Though the signature says not to return null, if an empty string is returned it changes the behaviour
        public string? Format(string? format, object? arg, IFormatProvider? formatProvider)
#pragma warning restore 8766
        {
            if (!(arg is int) || string.IsNullOrWhiteSpace(format))
                return null;

            var formatParts = format!.Split(':');
            if (formatParts.Length != 2)
                return null;

            if (!string.Equals(formatParts[0], "p", StringComparison.OrdinalIgnoreCase))
                return null;

            var count = (int)arg;
            return formatParts[1].Plural(count);
        }
    }
}