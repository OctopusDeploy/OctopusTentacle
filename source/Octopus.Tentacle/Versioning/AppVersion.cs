using System;
using System.Reflection;

namespace Octopus.Tentacle.Versioning
{
    public class AppVersion
    {
        private readonly SemanticVersionInfo semanticVersionInfo;

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