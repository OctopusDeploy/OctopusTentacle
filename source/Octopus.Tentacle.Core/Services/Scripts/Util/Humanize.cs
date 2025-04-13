using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Tentacle.Core.Services.Scripts.Util
{
    public static class Humanize
    {
        public static string Plural(this string simpleNoun, int count = 2)
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

        public static string ReadableJoin<T>(this IEnumerable<T> list, string junction = "and")
        {
            if (list == null) throw new ArgumentNullException("list");

            var result = new StringBuilder();
            object? prev = null;

            string separator = "", final = "";
            var enumerator = list.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (prev != null)
                {
                    result.Append(separator);
                    result.Append(prev);
                    separator = ", ";
                    final = " " + junction + " ";
                }

                prev = enumerator.Current;
            }

            if (prev != null)
            {
                result.Append(final);
                result.Append(prev);
            }

            return result.ToString();
        }
    }
}