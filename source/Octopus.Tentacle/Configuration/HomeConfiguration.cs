using System;
using System.IO;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Configuration
{
    public class HomeConfiguration : IHomeConfiguration
    {
        internal const string OctopusHomeSettingName = "Octopus.Home";
        internal const string OctopusNodeCacheSettingName = "Octopus.Node.Cache";
        
        readonly IKeyValueStore? settings;
        readonly IApplicationInstanceSelector applicationInstanceSelector;

        public HomeConfiguration(IApplicationInstanceSelector applicationInstanceSelector)
        {
            settings = applicationInstanceSelector.Current.Configuration;
            this.applicationInstanceSelector = applicationInstanceSelector;
        }

        public string? HomeDirectory
        {
            get
            {
                var value = settings?.Get<string?>(OctopusHomeSettingName);
                return value == null ? null : EnsureRootedPath(value);
            }
        }

        public void WriteTo(IWritableKeyValueStore outputStore)
        {
            outputStore.Set(OctopusHomeSettingName, HomeDirectory);
        }

        string? EnsureRootedPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            // Its possible that this code path is being run before there is any instance yet configured.
            // Rather than making assumptions, fall back to missing.
            if (!applicationInstanceSelector.CanLoadCurrentInstance())
            {
                return null;
            }
            
            var relativeRoot = Path.GetDirectoryName(applicationInstanceSelector.Current.ConfigurationPath);
            if (relativeRoot == null)
            {
                throw new Exception($"Unable to load configuration directory details. "
                    + $"Unable to determine path from configuration path '{applicationInstanceSelector.Current.ConfigurationPath}'");
            }
            
            return Path.Combine(relativeRoot, path);
        }
    }
    

    public class WritableHomeConfiguration : HomeConfiguration, IWritableHomeConfiguration
    {
        readonly IWritableKeyValueStore? settings;

        public WritableHomeConfiguration(IApplicationInstanceSelector applicationInstanceSelector, IWritableKeyValueStore? writableConfiguration = null) : base(applicationInstanceSelector)
        {
            settings = writableConfiguration ?? applicationInstanceSelector.Current.WritableConfiguration;
        }

        public bool SetHomeDirectory(string? homeDirectory)
            => settings?.Set(OctopusHomeSettingName, homeDirectory) ?? false;
    }
}