using System.Collections.Generic;
using Octopus.Client.Model;

namespace Octopus.Shared
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> ToEnumerable<T>(this IEnumerable<T> source)
        {
            return source;
        }

        public static ReferenceCollection ToReferenceCollection(this IEnumerable<string> source)
        {
            return new ReferenceCollection(source);
        }
    }
}