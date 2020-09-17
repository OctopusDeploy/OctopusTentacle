using System;

namespace Octopus.Tentacle.Versioning
{
    public sealed class AssemblyNuGetVersionAttribute : Attribute
    {
        public AssemblyNuGetVersionAttribute(string nuGetVersion)
        {
            NuGetVersion = nuGetVersion;
        }

        public string NuGetVersion { get; }
    }
}