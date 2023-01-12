using System;
using System.Reflection;
using NuGet.Versioning;

namespace Octopus.Tentacle.Versioning
{
    /// <summary>
    /// http://gitversion.readthedocs.org/en/latest/more-info/variables/
    /// </summary>
    public class SemanticVersionInfo
    {
        public SemanticVersionInfo(Assembly assembly)
        {
            SemanticVersion = SemanticVersion.Parse(assembly.GetInformationalVersion() ?? "0.0.0");
            MajorMinorPatch = SemanticVersion.ToString("V", new VersionFormatter());
            BranchName = assembly.GetCustomAttribute<AssemblyGitBranchAttribute>()!.BranchName;
            NuGetVersion = assembly.GetCustomAttribute<AssemblyNuGetVersionAttribute>()!.NuGetVersion;
        }

        /// <summary>
        /// The SemanticVersion parsed from the AssemblyInformationalVersion
        /// </summary>
        public SemanticVersion SemanticVersion { get; }

        /// <summary>
        /// Example: "3.0.0"
        /// </summary>
        public string MajorMinorPatch { get; }

        /// <summary>
        /// Example: "release/3.0.0"
        /// </summary>
        public string BranchName { get; }

        /// <summary>
        /// Example: "3.0.0-beta0001"
        /// </summary>
        public string NuGetVersion { get; }
    }
}