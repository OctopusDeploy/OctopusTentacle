using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Util
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TElement> Apply<TElement>(this IEnumerable<TElement> source, Action<TElement> apply)
        {
            foreach (var item in source)
            {
                apply(item);
                yield return item;
            }
        }

        public static IEnumerable<TElement> NotNull<TElement>(this IEnumerable<TElement> source)
        {
            return source.Where(item => item != null);
        }

        public static IEnumerable<string> NotNullOrWhiteSpace(this IEnumerable<string> source)
        {
            return source.Where(item => !string.IsNullOrWhiteSpace(item));
        }

        public static IEnumerable<TValue[]> Permutations<TKey, TValue>(this IEnumerable<TKey> keys, Func<TKey, IEnumerable<TValue>> selector)
        {
            var keyArray = keys.ToArray();
            if (keyArray.Length < 1)
                return Enumerable.Empty<TValue[]>();
            TValue[] values = new TValue[keyArray.Length];
            return Permutations(keyArray, 0, selector, values);
        }

        static IEnumerable<TValue[]> Permutations<TKey, TValue>(TKey[] keys, int index, Func<TKey, IEnumerable<TValue>> selector, TValue[] values)
        {
            var key = keys[index];
            foreach (var value in selector(key))
            {
                values[index] = value;
                if (index < keys.Length - 1)
                {
                    foreach (var array in Permutations(keys, index + 1, selector, values))
                        yield return array;
                }
                else
                {
                    yield return values.ToArray(); // Clone the array;
                }
            }
        }

        public static IEnumerable<IEnumerable<T>> BatchWithBlockSize<T>(this IEnumerable<T> source, int blockSize)
        {
            return source
                .Select((x, index) => new { x, index })
                .GroupBy(x => x.index / blockSize, y => y.x);
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            return source == null || !source.Any();
        }

        public static bool None<T>(this IEnumerable<T> items) => !items.Any();

        public static bool None<T>(this IEnumerable<T> items, Func<T, bool> predicate) => !items.Any(predicate);

        public static IEnumerable<T> TakeUntilIncluding<T>(this IEnumerable<T> list, Func<T, bool> predicate)
        {
            foreach (T el in list)
            {
                yield return el;
                if (predicate(el))
                    yield break;
            }
        }

        public static bool Missing<T>(this IEnumerable<T> items, T item) => !items.Contains(item);

        public static bool Missing<T>(this IEnumerable<T> items, T item, IEqualityComparer<T> equalityComparer) => !items.Contains(item, equalityComparer);

        public static T OnlyOrDefault<T>(this IEnumerable<T> source)
        {
            using (var e = source.GetEnumerator())
            {
                if (!e.MoveNext())
                    return default;
                var result = e.Current;
                if (e.MoveNext())
                    return default;
                return result;
            }
        }
    }
}
