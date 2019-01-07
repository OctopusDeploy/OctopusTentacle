using System;
using System.IO;
using System.Reflection;
using Octopus.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class HomeConfiguration : IHomeConfiguration
    {
        readonly ApplicationName application;
        readonly IKeyValueStore settings;
        readonly string defaultHome;

        public HomeConfiguration(ApplicationName application, IKeyValueStore settings)
        {
            this.application = application;
            this.settings = settings;

            defaultHome = Path.Combine(Directory.GetDirectoryRoot(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)), "Octopus");
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
    }
}