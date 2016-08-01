using Octopus.Server.Extensibility.Configuration;

namespace Octopus.Shared.Configuration
{
    public class AzurePowerShellModuleConfiguration : IAzurePowerShellModuleConfiguration
    {
        readonly IKeyValueStore settings;
        const string Key = "Octopus.Azure.PowerShellModule";

        public AzurePowerShellModuleConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public string AzurePowerShellModule
        {
            get { return settings.Get(Key); }
            set { settings.Set(Key, value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}