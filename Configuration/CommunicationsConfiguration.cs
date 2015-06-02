using System;

namespace Octopus.Shared.Configuration
{
    public class CommunicationsConfiguration : ICommunicationsConfiguration
    {
        readonly IKeyValueStore settings;

        public CommunicationsConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
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