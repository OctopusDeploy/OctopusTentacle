using System;
using System.IO;
using Octopus.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Configuration
{
    public class HomeConfiguration : IHomeConfiguration
    {
        internal const string OctopusHomeSettingName = "Octopus.Home";
        internal const string OctopusNodeCacheSettingName = "Octopus.Node.Cache";

        private readonly ApplicationName application;
        private readonly IKeyValueStore settings;
        private readonly IApplicationInstanceSelector applicationInstanceSelector;

        public HomeConfiguration(ApplicationName application,
            IKeyValueStore settings,
            IApplicationInstanceSelector applicationInstanceSelector)
        {
            this.application = application;
            this.settings = settings;
            this.applicationInstanceSelector = applicationInstanceSelector;
        }

        public string? ApplicationSpecificHomeDirectory => HomeDirectory == null ? null : Path.Combine(HomeDirectory, application.ToString());

        public string? HomeDirectory
        {
            get
            {
                var value = settings.Get<string?>(OctopusHomeSettingName);
                return value == null ? null : EnsureRootedPath(value);
            }
        }

        private string? EnsureRootedPath(string path)
        {
            if (Path.IsPathRooted(path)) return path;

            // Its possible that this code path is being run before there is any instance yet configured.
            // Rather than making assumptions, fall back to missing.
            if (!applicationInstanceSelector.CanLoadCurrentInstance()) return null;

            var relativeRoot = Path.GetDirectoryName(applicationInstanceSelector.Current.ConfigurationPath);
            if (relativeRoot == null)
                throw new Exception("Unable to load configuration directory details. "
                    + $"Unable to determine path from configuration path '{applicationInstanceSelector.Current.ConfigurationPath}'");

            return Path.Combine(relativeRoot, path);
        }
    }

    public class WritableHomeConfiguration : HomeConfiguration, IWritableHomeConfiguration
    {
        private readonly IWritableKeyValueStore settings;

        public WritableHomeConfiguration(ApplicationName application, IWritableKeyValueStore writableConfiguration, IApplicationInstanceSelector applicationInstanceSelector) : base(application, writableConfiguration, applicationInstanceSelector)
        {
            settings = writableConfiguration;
        }

        public bool SetHomeDirectory(string? homeDirectory)
        {
            return settings.Set(OctopusHomeSettingName, homeDirectory);
        }
    }
}