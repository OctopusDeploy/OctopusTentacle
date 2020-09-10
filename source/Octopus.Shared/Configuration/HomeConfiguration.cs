using System.IO;
using Octopus.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class HomeConfiguration : IHomeConfiguration
    {
        internal const string OctopusHomeSettingName = "Octopus.Home";
        internal const string OctopusNodeCacheSettingName = "Octopus.Node.Cache";

        readonly ApplicationName application;
        readonly IKeyValueStore settings;

        public HomeConfiguration(ApplicationName application, IKeyValueStore settings)
        {
            this.application = application;
            this.settings = settings;
        }
        
        public string? ApplicationSpecificHomeDirectory => HomeDirectory == null ? null : Path.Combine(HomeDirectory, application.ToString());

        public string? HomeDirectory
        {
            get
            {
                var value = settings.Get<string?>(OctopusHomeSettingName, null);
                if (value != null && !Path.IsPathRooted(value))
                    value = PathHelper.ResolveRelativeDirectoryPath(value);
                return value;
            }
        }

        public string? CacheDirectory
        {
            get
            {
                var value = settings.Get<string?>(OctopusNodeCacheSettingName, null);
                if (value == null)
                    return ApplicationSpecificHomeDirectory;
                
                if (!Path.IsPathRooted(value))
                    value = PathHelper.ResolveRelativeDirectoryPath(value);
                
                return value;
            }
        }
    }
}