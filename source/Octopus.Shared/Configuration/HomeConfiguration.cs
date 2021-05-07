using System;
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

        public string ApplicationSpecificHomeDirectory => Path.Combine(HomeDirectory, application.ToString());

        public string HomeDirectory
        {
            get
            {
                var value = settings.Get<string?>(OctopusHomeSettingName);
                if (value != null && !Path.IsPathRooted(value))
                    value = PathHelper.ResolveRelativeDirectoryPath(value);
                return value ?? Environment.CurrentDirectory;
            }
        }

        public string? CacheDirectory
        {
            get
            {
                var value = settings.Get<string?>(OctopusNodeCacheSettingName);
                if (value == null)
                    return ApplicationSpecificHomeDirectory;

                if (!Path.IsPathRooted(value))
                    value = PathHelper.ResolveRelativeDirectoryPath(value);

                return value;
            }
        }
    }

    public class WritableHomeConfiguration : HomeConfiguration, IWritableHomeConfiguration
    {
        readonly IWritableKeyValueStore settings;

        public WritableHomeConfiguration(ApplicationName application, IWritableKeyValueStore writableConfiguration) : base(application, writableConfiguration)
        {
            settings = writableConfiguration;
        }

        public bool SetHomeDirectory(string? homeDirectory)
            => settings.Set(OctopusHomeSettingName, homeDirectory);

        public bool SetCacheDirectory(string? cacheDirectory)
            => settings.Set(OctopusNodeCacheSettingName, cacheDirectory);
    }
}