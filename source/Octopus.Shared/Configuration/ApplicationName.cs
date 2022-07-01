using System;
using System.ComponentModel;

namespace Octopus.Shared.Configuration
{
    public enum ApplicationName
    {
        [Description("Octopus Server")]
        OctopusServer,

        [Description("Tentacle")]
        Tentacle
    }
}