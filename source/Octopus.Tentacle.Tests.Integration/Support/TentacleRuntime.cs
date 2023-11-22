using System;
using System.ComponentModel;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class DefaultTentacleRuntime
    {
        public const TentacleRuntime Value =
#if NETFRAMEWORK
            TentacleRuntime.Framework48;
#elif NET6_0
            TentacleRuntime.DotNet6;
#elif NET8_0
            TentacleRuntime.DotNet8;
#endif // no default "else" case; the code should intentionally not compile on other target frameworks unless we come and update it
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
