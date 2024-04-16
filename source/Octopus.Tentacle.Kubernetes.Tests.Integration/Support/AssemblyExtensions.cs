using System.Reflection;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Support;

public static class AssemblyExtensions
{
    public static Stream GetManifestResourceStreamFromPartialName(this Assembly assembly, string filename)
    {        
        var valuesFileName = assembly.GetManifestResourceNames().Single(n => n.Contains(filename, StringComparison.OrdinalIgnoreCase));
        return assembly.GetManifestResourceStream(valuesFileName)!;
    }
}