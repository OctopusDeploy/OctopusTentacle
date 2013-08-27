using System;
using System.IO;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;

namespace Octopus.Shared.Configuration
{
    public class HomeConfiguration : IHomeConfiguration, Autofac.IStartable
    {
        readonly ILog log = Log.Octopus();
        readonly IKeyValueStore settings;
        readonly string defaultHome;

        public HomeConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
            defaultHome = Path.Combine(Directory.GetDirectoryRoot(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)), "Octopus");
        }

        public string HomeDirectory
        {
            get
            {
                var value = settings.Get("Octopus.Home", defaultHome);
                if (!Path.IsPathRooted(value))
                {
                    value = PathHelper.ResolveRelativeDirectoryPath(value);
                }

                return value;
            }
            set { settings.Set("Octopus.Home", value); }
        }

        public void Start()
        {
            var resolvedHomeDirectory = HomeDirectory;
            log.VerboseFormat("Using home directory: {0}", resolvedHomeDirectory);
        }
    }
}