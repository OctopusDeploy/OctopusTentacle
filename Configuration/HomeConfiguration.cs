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
        readonly string defaultHome;

        public HomeConfiguration(ApplicationName application, IKeyValueStore settings)
        {
            this.application = application;
            this.settings = settings;

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (string.IsNullOrWhiteSpace(programFiles)) // 32 bit
                programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            defaultHome = Path.Combine(Directory.GetDirectoryRoot(programFiles), "Octopus");
        }

        public string ApplicationSpecificHomeDirectory => HomeDirectory == null ? null : Path.Combine(HomeDirectory, application.ToString());

        public string HomeDirectory
        {
            get
            {
                var value = settings.Get("Octopus.Home", null);
                if (value != null && !Path.IsPathRooted(value))
                    value = PathHelper.ResolveRelativeDirectoryPath(value);
                return value;
            }
            set { settings.Set("Octopus.Home", value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}