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
            TentacleRuntime.DotNet10;
#endif
    }

    public enum TentacleRuntime
    {
        [Description(RuntimeDetection.Framework48)]
        Framework48,
        
        [Description(RuntimeDetection.DotNet10)]
        DotNet10,
    }
}
