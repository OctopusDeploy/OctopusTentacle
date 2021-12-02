using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Octopus.Shared.Util
{
    public class SlugGenerator
    {
        public static string GenerateSlug(string input, Func<string, bool> isInUse)
        {
            input = ConvertToLowercaseAndDashes(input);

            if (string.IsNullOrWhiteSpace(input))
            {
                input = "blue-ring";
            }

            return Uniquifier.UniquifyString(input, isInUse);
        }

        static string ConvertToLowercaseAndDashes(string value)
        {
            value = value ?? string.Empty;
            value = value.ToLower();
            value = Regex.Replace(value, "\\s", "-");
            value = new string(value.Select(x => char.IsLetterOrDigit(x) || x == '-' ? x : '-').ToArray());
            value = Regex.Replace(value, "-+", "-");
            value = value.Trim('-', '/');
            return value;
        }
    }
}
