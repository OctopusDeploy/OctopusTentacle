using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace
public static class ListExtensions
// ReSharper restore CheckNamespace
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
}