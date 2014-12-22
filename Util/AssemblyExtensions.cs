using System;
using System.Linq;
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
            var attribute = assembly.GetCustomAttributes(true).OfType<AssemblyFileVersionAttribute>().FirstOrDefault();
            if (attribute != null)
            {
                return attribute.Version;
            }

            return "Unknown";
        }

        public static string GetInformationalVersion(this Assembly assembly)
        {
            var attribute = assembly.GetCustomAttributes(true).OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
            if (attribute != null)
            {
                return attribute.InformationalVersion;
            }

            return "Unknown";
        }
    }
}
