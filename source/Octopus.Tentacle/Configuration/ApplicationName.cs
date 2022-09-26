using System;
using System.ComponentModel;

namespace Octopus.Tentacle.Configuration
{
    public enum ApplicationName
    {
        [Description("Octopus Server")] OctopusServer,

        [Description("Tentacle")] Tentacle
    }
}