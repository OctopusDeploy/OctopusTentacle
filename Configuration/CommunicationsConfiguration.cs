using System;
using System.IO;
using Octopus.Shared.Communications;

namespace Octopus.Shared.Configuration
{
    public class CommunicationsConfiguration : ICommunicationsConfiguration
    {
        readonly IKeyValueStore settings;
        readonly IHomeConfiguration homeConfiguration;

        public CommunicationsConfiguration(IKeyValueStore settings, IHomeConfiguration homeConfiguration)
        {
            this.settings = settings;
            this.homeConfiguration = homeConfiguration;

            if (string.IsNullOrWhiteSpace(Squid))
            {
                var newSquid = "SQ-" +
                               Environment.MachineName + "-" +
                               Guid.NewGuid().GetHashCode().ToString("X8");
                Squid = NormalizeSquid(newSquid);

                settings.Save(); // For server this will fail I think; needs to be set on installation.
            }
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
            get { return EnsureExists(Path.Combine(homeConfiguration.HomeDirectory, "Actors")); }
        }

        public string MessagesDirectory
        {
            get { return EnsureExists(Path.Combine(homeConfiguration.HomeDirectory, "Messages")); }
        }

        static string EnsureExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }
    }
}