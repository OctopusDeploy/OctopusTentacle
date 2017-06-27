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
                yield break;
            TValue[] values = new TValue[keyArray.Length];
            foreach (var array in Permutations(keyArray, 0, selector, values))
                yield return array;
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

    }
}
