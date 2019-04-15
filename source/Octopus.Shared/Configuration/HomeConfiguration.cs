using System;
using System.IO;
using Octopus.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class HomeConfiguration : IHomeConfiguration
    {
        readonly ApplicationName application;
        readonly IKeyValueStore settings;
        

        public HomeConfiguration(ApplicationName application, IKeyValueStore settings)
        {
            this.application = application;
            this.settings = settings;
        }
        
        public string ApplicationSpecificHomeDirectory => HomeDirectory == null ? null : Path.Combine(HomeDirectory, application.ToString());

        public string HomeDirectory
        {
            get
            {
                var value = settings.Get<string>("Octopus.Home", null);
                if (value != null && !Path.IsPathRooted(value))
                    value = PathHelper.ResolveRelativeDirectoryPath(value);
                return value;
            }
            set => settings.Set("Octopus.Home", value);
        }
        
        public string CacheDirectory
        {
            get
            {
                var value = settings.Get<string>("Octopus.Node.Cache", null);
                if (value == null)
                    return ApplicationSpecificHomeDirectory;
                
                if (!Path.IsPathRooted(value))
                    value = PathHelper.ResolveRelativeDirectoryPath(value);
                
                return value;
            }
            set => settings.Set("Octopus.Node.Cache", value);
        }
    }
}