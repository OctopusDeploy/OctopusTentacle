using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Octopus.Shared.Versioning
{
    /// <summary>
    /// http://gitversion.readthedocs.org/en/latest/more-info/variables/
    /// </summary>
    public class SemanticVersionInfo
    {
        readonly Lazy<Dictionary<string, string>> gitVersionDetails;

        public SemanticVersionInfo(Assembly assembly)
        {
            gitVersionDetails = new Lazy<Dictionary<string, string>>(() => LoadGitVersionDetails(assembly));
        }

        public SemanticVersionInfo(Dictionary<string, string> gitVersionDetails)
        {
            this.gitVersionDetails = new Lazy<Dictionary<string, string>>(() => gitVersionDetails);
        }

        static Dictionary<string, string> LoadGitVersionDetails(Assembly assembly)
        {
            var gitVersionInformationType = assembly.GetType(assembly.GetName() + ".GitVersionInformation", throwOnError: false) ?? assembly.DefinedTypes.FirstOrDefault(t => t.Name == "GitVersionInformation");
            if (gitVersionInformationType == null) throw new Exception($"Couldn't find the GitVersionInformation class defined in the Assembly {assembly.GetName().Name}");
            var fields = gitVersionInformationType.GetFields();
            return fields.ToDictionary(field => field.Name, field => (string)field.GetValue(null));
        }

        public string this[string key] => gitVersionDetails.Value[key];

        /// <summary>
        /// The Major version number from {Major}.{Minor}.{Patch}. Example: 3
        /// </summary>
        public int Major => int.Parse(gitVersionDetails.Value["Major"]);
        /// <summary>
        /// The Minor version number from {Major}.{Minor}.{Patch}. Example: 0
        /// </summary>
        public int Minor => int.Parse(gitVersionDetails.Value["Minor"]);
        /// <summary>
        /// The Patch version number from {Major}.{Minor}.{Patch}. Example: 0
        /// </summary>
        public int Patch => int.Parse(gitVersionDetails.Value["Patch"]);
        /// <summary>
        /// Example: "beta.1"
        /// </summary>
        public string PreReleaseTag => gitVersionDetails.Value["PreReleaseTag"];
        /// <summary>
        /// Example: "-beta.1"
        /// </summary>
        public string PreReleaseTagWithDash => gitVersionDetails.Value["PreReleaseTagWithDash"];
        /// <summary>
        /// Example: 1
        /// </summary>
        public int BuildMetaData => int.Parse(gitVersionDetails.Value["BuildMetaData"]);
        /// <summary>
        /// Example: "1.Branch.release/3.0.0.Sha.28c853159a46b5a87e6cc9c4f6e940c59d6bc68a"
        /// </summary>
        public string FullBuildMetaData => gitVersionDetails.Value["FullBuildMetaData"];
        /// <summary>
        ///  Example: "3.0.0"
        /// </summary>
        public string MajorMinorPatch => gitVersionDetails.Value["MajorMinorPatch"];
        /// <summary>
        /// Example: "3.0.0-beta.1"
        /// </summary>
        public string SemVer => gitVersionDetails.Value["SemVer"];
        /// <summary>
        /// Example: "3.0.0-beta1"
        /// </summary>
        public string LegacySemVer => gitVersionDetails.Value["LegacySemVer"];
        /// <summary>
        /// Example: "3.0.0-beta0001"
        /// </summary>
        public string LegacySemVerPadded => gitVersionDetails.Value["LegacySemVerPadded"];
        /// <summary>
        /// The {Major}.{Minor}.0.0 Example: "3.0.0.0"
        /// </summary>
        /// <remarks>This is a common approach that gives you the ability to roll out hot fixes to your assembly without breaking existing applications that may be referencing it. You are still able to get the full version number if you need to by looking at its file version number.</remarks>
        public string AssemblySemVer => gitVersionDetails.Value["AssemblySemVer"];
        /// <summary>
        /// Example: "3.0.0-beta.1+1"
        /// </summary>
        public string FullSemVer => gitVersionDetails.Value["FullSemVer"];
        /// <summary>
        /// Example: "3.0.0-beta.1+1.Branch.release/3.0.0.Sha.28c853159a46b5a87e6cc9c4f6e940c59d6bc68a"
        /// </summary>
        public string InformationalVersion => gitVersionDetails.Value["InformationalVersion"];
        /// <summary>
        /// Example: "release/3.0.0"
        /// </summary>
        public string BranchName => gitVersionDetails.Value["BranchName"];
        /// <summary>
        /// Example: "28c853159a46b5a87e6cc9c4f6e940c59d6bc68a"
        /// </summary>
        public string Sha => gitVersionDetails.Value["Sha"];
        /// <summary>
        /// Example: "3.0.0-beta0001"
        /// </summary>
        public string NuGetVersionV2 => gitVersionDetails.Value["NuGetVersionV2"];
        /// <summary>
        /// Example: "3.0.0-beta0001"
        /// </summary>
        public string NuGetVersion => gitVersionDetails.Value["NuGetVersion"];
    }
}