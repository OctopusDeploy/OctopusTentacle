using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Octopus.Platform.Util
{
    public class PathSegmentValidator
    {
        static readonly HashSet<char> NotAllowed = new HashSet<char>();

        static PathSegmentValidator()
        {
            NotAllowed.AddRange(Path.GetInvalidPathChars());
            NotAllowed.AddRange(Path.GetInvalidFileNameChars());
            NotAllowed.AddRange(new[] {'{', '}'});
        }

        public static bool IsValid(string name)
        {
            return !name.Any(n => NotAllowed.Contains(n));
        }

        public static string MakeValid(string name)
        {
            return new string(
                name.Select(c => NotAllowed.Contains(c) ? '-' : c).ToArray());
        }
    }
}