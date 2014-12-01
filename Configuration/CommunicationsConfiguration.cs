using System;
using System.IO;
using Octopus.Platform.Deployment.Configuration;

namespace Octopus.Shared.Configuration
{
    public class CommunicationsConfiguration : ICommunicationsConfiguration, ITcpServerCommunicationsConfiguration
    {
        readonly IKeyValueStore settings;
        readonly IHomeConfiguration homeConfiguration;

        public const string SquidSettingKey = "Octopus.Communications.Squid";

        public CommunicationsConfiguration(IKeyValueStore settings, IHomeConfiguration homeConfiguration)
        {
            this.settings = settings;
            this.homeConfiguration = homeConfiguration;

            if (string.IsNullOrWhiteSpace(Squid))
            {
                Squid = Communications.Identity.Squid.NewSquid();
                settings.Save();
            }
        }

        public string Squid
        {
            get { return settings.Get(SquidSettingKey); }
            set { settings.Set(SquidSettingKey, value); }
        }

        public string ActorStateDirectory
        {
            get { return EnsureExists(Path.Combine(homeConfiguration.ApplicationSpecificHomeDirectory, "Actors")); }
        }

        public string StreamsDirectory
        {
            get { return EnsureExists(Path.Combine(homeConfiguration.ApplicationSpecificHomeDirectory, "Streams")); }
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

        public long FileTransferChunkSizeBytes
        {
            get { return settings.Get("Octopus.Communications.FileTransferChunkSizeBytes", 1024 * 1024); }
            set { settings.Set("Octopus.Communications.FileTransferChunkSizeBytes", value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}
