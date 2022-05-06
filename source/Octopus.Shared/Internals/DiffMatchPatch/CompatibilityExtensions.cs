using System;
using System.Collections.Generic;

namespace Octopus.Shared.Internals.DiffMatchPatch
{
    static class CompatibilityExtensions
    {
        // JScript splice function
        public static List<T> Splice<T>(this List<T> input,
            int start,
            int count,
            params T[] objects)
        {
            var deletedRange = input.GetRange(start, count);
            input.RemoveRange(start, count);
            input.InsertRange(start, objects);

            return deletedRange;
        }

        // Java substring function
        public static string JavaSubstring(this string s, int begin, int end)
            => s.Substring(begin, end - begin);
    }

    /**-
     * The data structure representing a diff is a List of Diff objects:
     * {Diff(Operation.DELETE, "Hello"), Diff(Operation.INSERT, "Goodbye"),
     *  Diff(Operation.EQUAL, " world.")}
     * which means: delete "Hello", add "Goodbye" and keep " world."
     */

    /**
     * Class representing one diff operation.
     */

    /**
     * Class representing one patch operation.
     */

    /**
     * Class containing the diff, match and patch methods.
     * Also Contains the behaviour settings.
     */
}