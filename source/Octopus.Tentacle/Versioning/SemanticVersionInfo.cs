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
        readonly Lazy<SemanticVersion> semanticVersion;

        public SemanticVersionInfo(Assembly assembly)
        {
            semanticVersion = new Lazy<SemanticVersion>(() => LoadSemanticVersionDetails(assembly));
        }

        static SemanticVersion LoadSemanticVersionDetails(Assembly assembly)
        {
            return SemanticVersion.Parse(assembly.GetInformationalVersion());
        }

        /// <summary>
        /// The SemanticVersion parsed from the AssemblyInformationalVersion
        /// </summary>
        public SemanticVersion SemanticVersion => semanticVersion.Value;
        /// <summary>
        ///  Example: "3.0.0"
        /// </summary>
        public string MajorMinorPatch => semanticVersion.Value.ToString("V", new VersionFormatter());
        /// <summary>
        /// Example: "release/3.0.0"
        /// </summary>
        public string BranchName => VersionInformation.BranchName;
        /// <summary>
        /// Example: "3.0.0-beta0001"
        /// </summary>
        public string NuGetVersion => VersionInformation.NuGetVersion;
    }
}