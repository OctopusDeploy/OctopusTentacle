using System;

namespace Octopus.Shared.Util
{
    public static class UriExtensions
    {
        public static bool IsNullOrEmpty(this Uri uri)
        {
            return string.IsNullOrWhiteSpace(uri?.OriginalString);
        }
    }
}