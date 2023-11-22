using System;
using System.ComponentModel;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class DefaultTentacleRuntime
    {
        public const TentacleRuntime Value =
#if NETFRAMEWORK
            TentacleRuntime.Framework48;
#else
            TentacleRuntime.DotNet6; // TODO OE: NET 8
#endif
    }

    public enum TentacleRuntime
    {
        [Description(RuntimeDetection.DotNet6)]
        DotNet6,
        
        [Description(RuntimeDetection.Framework48)]
        Framework48,

        [Description(RuntimeDetection.DotNet8)]
        DotNet8,
    }
}
