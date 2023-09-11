using System;
using System.ComponentModel;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public enum TentacleRuntime
    {
        [Description("Default")]
        Default,
        
        [Description(RuntimeDetection.DotNet6)]
        DotNet6,
        
        [Description(RuntimeDetection.Framework48)]
        Framework48
    }
}
