using System;

namespace Octopus.Tentacle.Diagnostics.Util
{
    static class Humanize
    {
        internal static string Plural(this string simpleNoun, int count = 2)
        {
            if (simpleNoun == null) throw new ArgumentNullException("simpleNoun");
            if (count == 1) return simpleNoun;

            if (simpleNoun.EndsWith("ay") ||
                simpleNoun.EndsWith("ey") ||
                simpleNoun.EndsWith("iy") ||
                simpleNoun.EndsWith("oy") ||
                simpleNoun.EndsWith("uy"))
                return simpleNoun + "s";

            if (simpleNoun.EndsWith("y"))
                return simpleNoun.Substring(0, simpleNoun.Length - 1) + "ies";

            if (simpleNoun.EndsWith("ss"))
                return simpleNoun + "es";

            if (simpleNoun.EndsWith("s"))
                return simpleNoun;

            return simpleNoun + "s";
        }
    }
}