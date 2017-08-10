using System;
using System.Collections.Generic;
using System.Linq;


namespace Octopus.Shared.Util
{
    public static class ListExtensions
    {
        public static void RemoveWhere<TElement>(this IList<TElement> source, Func<TElement, bool> remove)
        {
            if (source == null)
                return;

            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (!remove(item))
                    continue;

                source.RemoveAt(i);
                i--;
            }
        }

        public static void AddRange<TElement>(this ICollection<TElement> source, IEnumerable<TElement> itemsToAdd)
        {
            if (itemsToAdd == null || source == null)
                return;

            foreach (var item in itemsToAdd)
            {
                source.Add(item);
            }
        }

        public static void AddRangeUnique<TElement>(this ICollection<TElement> source, IEnumerable<TElement> itemsToAdd)
        {
            if (itemsToAdd == null || source == null)
                return;

            foreach (var item in itemsToAdd.Where(item => !source.Contains(item)))
            {
                source.Add(item);
            }
        }     

        public static void ReplaceAll<TElement>(this ICollection<TElement> source, IEnumerable<TElement> itemsToAdd)
        {
            source.Clear();
            source.AddRange(itemsToAdd);
        }
    }
}