using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Tentacle.Util
{
    public static class Humanize
    {
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