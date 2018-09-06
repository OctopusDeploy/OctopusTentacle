using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Octopus.Manager.Tentacle.Util
{
    public static class StringExtensions
    {
        public static string QuoteIfHasSeperator(this string str)
            => str.Contains(",") || str.Contains(";") ? '"' + str + '"' : str;

        public static string[] SplitOnSeperators(this string str)
        {
            if (str == null)
                return new string[0];
                
            IEnumerable<string> DoSplit()
            {
                var sb = new StringBuilder();
                var inQuotes = false;
                foreach (var c in str)
                {
                    if (!inQuotes && (c == ',' || c == ';'))
                    {
                        yield return sb.ToString();
                        sb.Clear();
                    }
                    else if (c == '"')
                    {
                        inQuotes = !inQuotes;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }


                yield return sb.ToString();
            }

            return DoSplit().Select(s => s.Trim()).Where(s => s != "").ToArray();
        }
    }
}