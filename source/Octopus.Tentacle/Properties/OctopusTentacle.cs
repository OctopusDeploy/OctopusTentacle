using System;
using System.Reflection;
using Octopus.Client.Model;
using Octopus.Shared.Util;
using Octopus.Tentacle.Versioning;

namespace Octopus.Tentacle.Properties
{
    public static class OctopusTentacle
    {
        public static readonly Assembly Assembly = typeof(OctopusTentacle).Assembly;
        public static readonly string InformationalVersion = Assembly.GetInformationalVersion();
        public static readonly SemanticVersionInfo SemanticVersionInfo = new(Assembly);
        public static readonly SemanticVersion Version = new(SemanticVersionInfo.NuGetVersion);
        public static readonly string[] EnvironmentInformation = EnvironmentHelper.SafelyGetEnvironmentInformation();
    }
}