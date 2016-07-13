using System;

namespace Octopus.Shared.Startup
{
    public class ServiceConfigurationState
    {
        public ServiceConfigurationState()
        {
        }

        public ServiceConfigurationState(bool start, bool stop, bool reconfigure, bool install, bool uninstall, string username, string password)
        {
            Start = start;
            Stop = stop;
            Reconfigure = reconfigure;
            Install = install;
            Uninstall = uninstall;
            Username = username;
            Password = password;
        }

        public bool Start { get; set; }
        public bool Stop { get; set; }
        public bool Reconfigure { get; set; }
        public bool Install { get; set; }
        public bool Uninstall { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}