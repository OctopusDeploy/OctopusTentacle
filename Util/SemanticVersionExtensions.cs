using System.Text.RegularExpressions;
using Octopus.Shared.Versioning;

namespace Octopus.Shared.Util
{
    public static class SemanticVersionInfoExtensions
    {
        static readonly Regex AlphaTagMatch = new Regex("^alpha\\.*[0-9]*$");

        public static bool IsEarlyAccessProgram(this SemanticVersionInfo semanticVersion)
        {
            return AlphaTagMatch.IsMatch(semanticVersion.PreReleaseTag);
        }
    }
}
