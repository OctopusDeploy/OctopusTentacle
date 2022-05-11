using System;
using System.Collections.Generic;
// ReSharper disable once RedundantUsingDirective : Used in .CORE
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Shared.Util
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source)
        {
            return source ?? new List<T>();
        }
    }
}