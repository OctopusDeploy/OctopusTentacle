using System;
using System.Diagnostics;
using System.Reflection;

// ReSharper disable CheckNamespace

namespace Octopus.Shared.Util
{
    public static class AssemblyExtensions
// ReSharper restore CheckNamespace
    {
        public static string FullLocalPath(this Assembly assembly)
        {
            var codeBase = assembly.CodeBase;
            var uri = new UriBuilder(codeBase);
            var root = Uri.UnescapeDataString(uri.Path);
            root = root.Replace("/", "\\");
            return root;
        }

        public static string GetFileVersion(this Assembly assembly)
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.FullLocalPath());
            return fileVersionInfo.FileVersion;
        }

        public static string GetInformationalVersion(this Assembly assembly)
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.FullLocalPath());
            return fileVersionInfo.ProductVersion;
        }
    }
}