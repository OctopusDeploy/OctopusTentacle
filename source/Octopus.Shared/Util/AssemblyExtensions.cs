using System;
using System.Reflection;

namespace Octopus.Shared.Util
{
    public static class AssemblyExtensions
    {
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