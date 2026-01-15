using System.Reflection;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Versioning;
using Octopus.Versioning.Semver;

namespace Octopus.Tentacle.Properties
{
    public static class OctopusTentacle
    {
        public static readonly Assembly Assembly = typeof (OctopusTentacle).Assembly;
        public static readonly string InformationalVersion = Assembly.GetInformationalVersion() ?? "0.0.0";
        public static readonly SemanticVersionInfo SemanticVersionInfo = new(Assembly);
        public static readonly SemanticVersion Version = SemanticVersionInfo.SemanticVersion;
        public static readonly string[] EnvironmentInformation = EnvironmentHelper.SafelyGetEnvironmentInformation();
    }
}