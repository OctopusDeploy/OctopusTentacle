using System.Text.RegularExpressions;
using Octopus.Shared.Versioning;

namespace Octopus.Shared.Util
{
    public static class SemanticVersionInfoExtensions
    {
        public static bool IsEarlyAccessProgram(this SemanticVersionInfo semanticVersion)
        {
            return !string.IsNullOrWhiteSpace(semanticVersion.PreReleaseTag);
        }
    }
}
