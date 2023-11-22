using System;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class RuntimeDetection
    {
        public const string DotNet6 = "net6.0";
        public const string DotNet8 = "net8.0";
        public const string Framework48 = "net48";

        public static string GetCurrentRuntime() => Environment.Version.Major switch
        {
            8 => DotNet8,
            6 => DotNet6,
            4 => Framework48, // This is the last net framework
            _ => throw new NotSupportedException($"Unsupported runtime {Environment.Version.Major} for tentacle integration tests")
        };

        public static bool IsDotNet6 => GetCurrentRuntime() is DotNet6;
        public static bool IsDotNet8 => GetCurrentRuntime() is DotNet8;
        
        public static bool IsDotNet6Or8 => GetCurrentRuntime() is DotNet6 or DotNet8;
        public static bool IsFramework48 => GetCurrentRuntime() == Framework48;
    }
}
