using System;
using System.Collections.Generic;
using Nevermore.Contracts;

namespace Octopus.Core.Extensions
{
    public static class EnumerableExtensions
    {
        public static ReferenceCollection ToReferenceCollection(this IEnumerable<string> source)
        {
            return new ReferenceCollection(source);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer)
        {
            return new HashSet<T>(source, comparer);
        }

        public static string ToSingleQuotedCommaSeperated<T>(this IEnumerable<T> items)
        {
            var joined = string.Join("', '", items);
            return joined == "" ? "" : $"'{joined}'";
        }

        public static IEnumerable<T> ToEnumerable<T>(this T source)
        {
            return new T[] { source };
        }
    }
}