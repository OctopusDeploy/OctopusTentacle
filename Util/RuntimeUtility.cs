using System;

namespace Octopus.Shared.Util
{
    public class RuntimeUtility
    {
        public static bool IsNet45OrNewer()
        {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        } 
    }
}