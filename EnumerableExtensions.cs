using System;
using System.Collections.Generic;

namespace Octopus.Shared
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> ToEnumerable<T>(this IEnumerable<T> source)
        {
            return source;
        }
    }
}
