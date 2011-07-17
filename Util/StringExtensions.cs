using System;
using System.Linq;

public static class StringExtensions
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
