using System;
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
            if (text.Length > length)
            {
                text = text.Substring(0, length) + "...";
            }
            return text.Trim();
        }
    }
}