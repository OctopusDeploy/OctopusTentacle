using System;
using System.Linq;

namespace Octopus.Shared.Util
{
    public class PathSegmentValidator
    {
        public static bool IsValid(string name)
        {
            return name.All(t => char.IsLetterOrDigit(t) || t == '.' || t == ' ' || t == '-' || t == '_' || t == '#' || t == ',');
        }
    }
}