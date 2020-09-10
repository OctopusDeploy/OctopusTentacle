using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class ModifiableHomeConfiguration : HomeConfiguration, IModifiableHomeConfiguration
    {
        readonly IModifiableKeyValueStore settings;

        public ModifiableHomeConfiguration(ApplicationName application, IModifiableKeyValueStore settings) : base(application, settings)
        {
            this.settings = settings;
        }

        public void SetHomeDirectory(string? homeDirectory)
        {
            settings.Set<string?>(OctopusHomeSettingName, homeDirectory);
        }

        public void SetCacheDirectory(string cacheDirectory)
        {
            settings.Set<string?>(OctopusNodeCacheSettingName, cacheDirectory);
        }
    }
}