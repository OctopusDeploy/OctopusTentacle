using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once RedundantUsingDirective : Used in .CORE

namespace Octopus.Tentacle.Util
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source)
        {
            return source ?? new List<T>();
        }

        public static bool Any<T>(this T[] array)
            => array.Length > 0;

        public static bool None<T>(this T[] array)
            => array.Length == 0;

        public static bool None<T>(this IEnumerable<T> items, Func<T, bool> predicate)
            => !items.Any(predicate);

        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
            => source == null || !source.Any();

        public static IEnumerable<string> WhereNotNullOrWhiteSpace(this IEnumerable<string> source)
            => source.Where(item => !string.IsNullOrWhiteSpace(item));

        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> items)
            where T : class
            => items.Where(i => i != null)!;
    }
}