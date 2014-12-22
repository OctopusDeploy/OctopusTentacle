using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Octopus.Platform.Util
{
    public class SlugGenerator
    {
        public static string GenerateSlug(string input, Func<string, bool> isInUse)
        {
            input = ConvertToLowecaseAndDashes(input);

            return Uniquifier.UniquifyString(input, isInUse);
        }

        static string ConvertToLowecaseAndDashes(string value)
        {
            value = value ?? string.Empty;
            value = value.ToLower();
            value = Regex.Replace(value, "\\s", "-");
            value = new string(value.Select(x => (char.IsLetterOrDigit(x) || x == '-') ? x : '-').ToArray());
            value = Regex.Replace(value, "-+", "-");
            value = value.Trim('-', '/');
            return value;
        }
    }
}