using System.Reflection;
using Octopus.Shared.Util;
using Octopus.Shared.Versioning;

namespace Octopus.Manager.Core.Properties
{
    public static class OctopusManager
    {
        public static readonly Assembly Assembly = typeof(OctopusManager).Assembly;
        public static readonly string InformationalVersion = Assembly.GetInformationalVersion();
        public static readonly SemanticVersionInfo SemanticVersionInfo = new SemanticVersionInfo(Assembly);
    }
}