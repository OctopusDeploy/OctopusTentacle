using System.Reflection;
using Octopus.Shared.Util;

namespace Octopus.Shared.Versioning
{
    public class AppVersion
    {
        readonly SemanticVersionInfo semanticVersionInfo;

        public AppVersion(Assembly assembly)
            : this(assembly.GetSemanticVersionInfo())
        {
        }

        public AppVersion(SemanticVersionInfo semanticVersionInfo)
        {
            this.semanticVersionInfo = semanticVersionInfo;
        }

        public override string ToString()
        {
            return semanticVersionInfo.NuGetVersion;
        }
    }
}