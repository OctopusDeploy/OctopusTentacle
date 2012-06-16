using System;
using System.Globalization;

namespace Octopus.Shared.Util
{
    public class Uniquifier
    {
        public static string UniquifyString(string input, Func<string, bool> isInUse, string format = "-{0}", int startCounter = 1)
        {
            var result = input;
            var i = startCounter;
            while (isInUse(result))
            {
                result = input + string.Format(format, i);
                i++;
            }

            return result;
        }

        public static string UniquifyStringFriendly(string input, Func<string, bool> isInUse)
        {
            return UniquifyString(input, isInUse, " (#{0:n0})", 2);
        }
    }
}