using System;

namespace Octopus.Tentacle.Versioning
{
    public static class SemanticVersionInfoExtensions
    {
        public static bool IsEarlyAccessProgram(this SemanticVersionInfo semanticVersionInfo)
        {
            return semanticVersionInfo.SemanticVersion.IsPrerelease &&
                // Exclude local dev builds
                semanticVersionInfo.SemanticVersion.Major != 0;
        }
    }
}
