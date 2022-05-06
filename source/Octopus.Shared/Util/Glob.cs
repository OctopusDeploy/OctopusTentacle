using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Octopus.Shared.Util
{
    public static class Glob
    {
        public static Regex RegexifyGlob(string pattern)
        {
            var regex = string.Join(".*?", pattern.Split(new[] { '*' }, StringSplitOptions.None).Select(Regex.Escape));
            return new Regex("^" + regex + "$", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
    }
}