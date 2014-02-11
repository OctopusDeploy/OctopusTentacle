using System;
using System.IO;
using Octopus.Platform.Deployment.Configuration;

namespace Octopus.Shared.Configuration
{
    public class CommunicationsConfiguration : ICommunicationsConfiguration, ITcpServerCommunicationsConfiguration
    {
        readonly IKeyValueStore settings;
        readonly IHomeConfiguration homeConfiguration;

        public CommunicationsConfiguration(IKeyValueStore settings, IHomeConfiguration homeConfiguration)
        {
            this.settings = settings;
            this.homeConfiguration = homeConfiguration;

            if (string.IsNullOrWhiteSpace(Squid))
            {
                Squid = NewSquid();
                settings.Save();
            }
        }

        public static string NewSquid()
        {
            var newSquid = "SQ-" +
                           Environment.MachineName + "-" +
                           Guid.NewGuid().GetHashCode().ToString("X8");
            return NormalizeSquid(newSquid);
        }

        static string NormalizeSquid(string squid)
        {
            return squid.ToUpperInvariant();
        }

        public string Squid
        {
            get { return settings.Get("Octopus.Communications.Squid"); }
            set { settings.Set("Octopus.Communications.Squid", value); }
        }

        public string ActorStateDirectory
        {
            get { return EnsureExists(Path.Combine(homeConfiguration.ApplicationSpecificHomeDirectory, "Actors")); }
        }

        public string MessagesDirectory
        {
            get { return EnsureExists(Path.Combine(homeConfiguration.ApplicationSpecificHomeDirectory, "Messages")); }
        }

        static string EnsureExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public int ServicesPort
        {
            get { return settings.Get("Octopus.Communications.ServicesPort", 10943); }
            set { settings.Set("Octopus.Communications.ServicesPort", value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}
