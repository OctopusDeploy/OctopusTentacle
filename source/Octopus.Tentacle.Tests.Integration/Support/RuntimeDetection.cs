using System;
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class RuntimeDetection
    {
        // TODO OE: Better runtime detection
        public const string DotNet6 = "net6.0";
        public const string Framework48 = "net48";
        
        public static string GetCurrentRuntime()
        {
            // This wont work for future versions of dotnet
            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET 6.0"))
            {
                return DotNet6;
            }

            // This is the last net framework
            return Framework48;
        }

        // TODO OE: Do we need an 8.0 specific thing?
        public static bool IsDotNet60 => GetCurrentRuntime() == DotNet6;
        public static bool IsFramework48 => GetCurrentRuntime() == Framework48;
    }
}
