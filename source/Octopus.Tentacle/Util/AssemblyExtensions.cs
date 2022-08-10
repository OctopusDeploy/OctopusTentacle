using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Octopus.Tentacle.Util
{
    public static class AssemblyExtensions
    {
        public static string FullProcessPath(this Assembly assembly)
        {
            string GetProcessFileName(string path)
                => PlatformDetection.IsRunningOnWindows ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path);

            var fileName = assembly.GetName().Name;
            string processFileName;
            using (var currentProcess = Process.GetCurrentProcess())
            {
                processFileName = currentProcess.MainModule.FileName;
            }

            if (processFileName == null || !GetProcessFileName(processFileName).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                processFileName = Path.Combine(Path.GetDirectoryName(assembly.Location) ?? ".", $"{Path.GetFileNameWithoutExtension(assembly.Location)}{(PlatformDetection.IsRunningOnWindows ? ".exe" : string.Empty)}");

            return processFileName;
        }

        public static string FullLocalPath(this Assembly assembly)
        {
            var codeBase = assembly.CodeBase ?? throw new Exception($"Could not get code base for {assembly.FullName}");
            var uri = new UriBuilder(codeBase);
            var root = Uri.UnescapeDataString(uri.Path);
            if (PlatformDetection.IsRunningOnWindows)
                root = root.Replace("/", "\\");
            return root;
        }
    }
}