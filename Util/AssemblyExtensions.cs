using System;
using System.Reflection;

// ReSharper disable CheckNamespace
public static class AssemblyExtensions
// ReSharper restore CheckNamespace
{
    public static string FullLocalPath(this Assembly assembly)
    {
        return assembly.Location.Replace("file:///", "");
    }
}
