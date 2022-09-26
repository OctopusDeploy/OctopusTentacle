using System;
using System.Linq;

namespace Octopus.Manager.Tentacle.Util
{
    public static class StringExtensions
    {
        public static string FirstCharToUpper(this string input)
        {
            if (!string.IsNullOrEmpty(input))
                return input.First().ToString().ToUpper() + input.Substring(1);
            return input;
        }
    }
}