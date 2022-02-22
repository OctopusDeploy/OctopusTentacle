using System;
using System.Diagnostics;
using System.Reflection;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Versioning
{
    public static class AssemblyExtensions
    {
        public static string GetFileVersion(this Assembly assembly)
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.FullLocalPath());
            return fileVersionInfo.FileVersion;
        }

        public static string GetInformationalVersion(this Assembly assembly)
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fileVersionInfo.ProductVersion;
        }

        public static SemanticVersionInfo GetSemanticVersionInfo(this Assembly assembly)
        {
            return new SemanticVersionInfo(assembly);
        }
    }
}