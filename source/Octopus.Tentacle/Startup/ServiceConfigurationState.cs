using System;

namespace Octopus.Tentacle.Startup
{
    public class ServiceConfigurationState
    {
        public bool Start { get; set; }
        public bool Stop { get; set; }
        public bool Restart { get; set; }
        public bool Reconfigure { get; set; }
        public bool Install { get; set; }
        public bool Uninstall { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? DependOn { get; set; }
    }
}