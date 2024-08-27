using System;
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class RuntimeDetection
    {
        public const string Framework48 = "net48";
        public const string DotNet6 = "net6.0";
        public const string DotNet8 = "net8.0";

        public static string GetCurrentRuntime()
        {
            // Expected framework description values taken from here: 
            // https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription?view=net-8.0#remarks
            var frameworkDescription = RuntimeInformation.FrameworkDescription;

            if (frameworkDescription.StartsWith(".NET Core") || frameworkDescription.StartsWith(".NET Native"))
            {
                throw new NotSupportedException(".NET Core and .NET Native runtimes are not supported");
            }

            if (frameworkDescription.StartsWith(".NET Framework 4.8"))
            {
                return Framework48;
            }

            if (frameworkDescription.StartsWith(".NET 6"))
            {
                return DotNet6;
            }

            if (frameworkDescription.StartsWith(".NET 8"))
            {
                return DotNet8;
            }
            
            throw new NotSupportedException($"'{frameworkDescription}' is not supported");
        }

        public static bool IsDotNet => GetCurrentRuntime() == DotNet8;
        public static bool IsFramework48 => GetCurrentRuntime() == Framework48;
    }
}
