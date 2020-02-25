using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Octopus.Shared.Util
{
    public static class AssemblyExtensions
    {
        public static string FullProcessPath(this Assembly assembly)
        {
            var fileName = assembly.GetName().Name;
            var processFileName = Process.GetCurrentProcess().MainModule?.FileName;

            if (processFileName == null || !Path.GetFileNameWithoutExtension(processFileName).Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                processFileName = Path.Combine(Path.GetDirectoryName(assembly.Location) ?? ".", $"{Path.GetFileNameWithoutExtension(assembly.Location)}{(PlatformDetection.IsRunningOnWindows ? ".exe" : String.Empty)}");
            }

            return processFileName;
        }
        
        public static string FullLocalPath(this Assembly assembly)
        {
            var codeBase = assembly.CodeBase;
            var uri = new UriBuilder(codeBase);
            var root = Uri.UnescapeDataString(uri.Path);
            if(PlatformDetection.IsRunningOnWindows)
                root = root.Replace("/", "\\");
            return root;
        }
    }
}