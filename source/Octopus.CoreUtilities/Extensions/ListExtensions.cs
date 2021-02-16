using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.CoreUtilities.Extensions
{
    public static class ListExtensions
    {
        public static int RemoveWhere<TElement>(this IList<TElement>? source, Func<TElement, bool> remove)
        {
            if (source == null)
                return 0;

            var removedCount = 0;

            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (!remove(item))
                    continue;

                source.RemoveAt(i);
                i--;
                removedCount++;
            }

            return removedCount;
        }

        public static void AddRange<TElement>(this ICollection<TElement>? source, IEnumerable<TElement>? itemsToAdd)
        {
            if (itemsToAdd == null || source == null)
                return;

            foreach (var item in itemsToAdd)
                source.Add(item);
        }

        public static void AddRangeUnique<TElement>(this ICollection<TElement>? source, IEnumerable<TElement>? itemsToAdd)
        {
            if (itemsToAdd == null || source == null)
                return;

            foreach (var item in itemsToAdd.Where(item => !source.Contains(item)))
                source.Add(item);
        }

        public static void ReplaceAll<TElement>(this ICollection<TElement> source, IEnumerable<TElement> itemsToAdd)
        {
            source.Clear();
            source.AddRange(itemsToAdd);
        }
    }
}