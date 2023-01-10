﻿using System.Reflection;
using Octopus.Tentacle.Versioning;

namespace Octopus.Manager.Tentacle
{
    public static class TentacleManager
    {
        public static readonly Assembly Assembly = typeof(TentacleManager).Assembly;
        public static readonly string InformationalVersion = Assembly.GetInformationalVersion() ?? "0.0.0";
        public static readonly SemanticVersionInfo SemanticVersionInfo = new SemanticVersionInfo(Assembly);
    }
}