using System;
using System.Globalization;

namespace Octopus.Shared.Util
{
    public class Uniquifier
    {
        public static string UniquifyString(string input, Func<string, bool> isInUse)
        {
            var result = input;
            var i = 1;
            while (isInUse(result))
            {
                result = input + "-" + i.ToString(CultureInfo.InvariantCulture);
                i++;
            }

            return result;
        }
    }
}