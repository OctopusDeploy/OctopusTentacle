using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable CheckNamespace

namespace Octopus.Shared.Util
{
    public static class StringExtensions
    // ReSharper restore CheckNamespace
    {
        public static string FirstLineTrimmedTo(this string text, int length)
        {
            text = text ?? string.Empty;
            text = text.Split('\n').First();
            return text.LimitWithEllipses(length);
        }

        /// <summary>
        /// Substring but OK if shorter
        /// </summary>
        public static string Limit(this string str, int characterCount)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (str.Trim().Length <= characterCount) return str.Trim();
            else return str.Substring(0, characterCount).Trim();
        }

        /// <summary>
        /// Substring with elipses but OK if shorter, will take 3 characters off character count if necessary
        /// </summary>
        public static string LimitWithEllipses(this string str, int characterCount)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (characterCount < 5) return str.Limit(characterCount);       // Can't do much with such a short limit
            if (str.Trim().Length <= characterCount) return str.Trim();
            else return str.Substring(0, characterCount - 3) + "...";
        }

        public static string AndList(this string[] items)
        {
            if (items.Length == 1) return items[0];
            return string.Join(", ", items.Take(items.Length - 1)) + " and " + items.Last();
        }

        public static string CommaSeperate(this IEnumerable<object> items) => string.Join(", ", items);
        public static string NewLineSeperate(this IEnumerable<object> items) => string.Join(Environment.NewLine, items);

        public static string ConstructQueryString(this System.Collections.Specialized.NameValueCollection Params)
        {
            var items = new List<string>();
            foreach (string name in Params)
                items.Add(String.Concat(name, "=", System.Web.HttpUtility.UrlEncode(Params[name])));
            return string.Join("&", items.ToArray());
        }
    }
}