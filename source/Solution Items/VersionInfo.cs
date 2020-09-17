// This file will be updated by our build process at compile time.

using System;
using System.Reflection;
using Octopus.Tentacle;

[assembly: AssemblyVersion(VersionInformation.AssemblyVersion)]
[assembly: AssemblyInformationalVersion(VersionInformation.AssemblyInformationalVersion)]
[assembly: AssemblyFileVersion(VersionInformation.AssemblyFileVersion)]

namespace Octopus.Tentacle
{
    static class VersionInformation
    {
        public const string AssemblyVersion = "0.0.0.0";
        public const string AssemblyFileVersion = "0.0.0.0";
        public const string AssemblyInformationalVersion = "0.0.0-local";
        public const string BranchName = "UNKNOWNBRANCH";
        public const string NuGetVersion = "0.0.0-local";
    }
}