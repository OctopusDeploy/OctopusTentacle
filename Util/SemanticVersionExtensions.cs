using Octopus.Shared.Versioning;

namespace Octopus.Shared.Util
{
    public static class SemanticVersionInfoExtensions
    {
        public static bool IsEarlyAccessProgram(this SemanticVersionInfo semanticVersionInfo)
        {
            return semanticVersionInfo.SemanticVersion.IsPrerelease;
        }
    }
}
