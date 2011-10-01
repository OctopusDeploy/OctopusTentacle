using System;
using System.Linq;
using System.Reflection;

// ReSharper disable CheckNamespace
public static class AssemblyExtensions
// ReSharper restore CheckNamespace
{
    public static string FullLocalPath(this Assembly assembly)
    {
        return assembly.Location.Replace("file:///", "");
    }

    public static string GetFileVersion(this Assembly assembly)
    {
        return assembly.GetCustomAttributes(true).OfType<AssemblyFileVersionAttribute>().First().Version;
    }
}
