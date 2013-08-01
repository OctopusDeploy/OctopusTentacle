using System;
using System.IO;

namespace Octopus.Shared.Configuration
{
    public class FileStorageConfiguration : IFileStorageConfiguration
    {
        readonly IHomeConfiguration homeConfiguration;

        public FileStorageConfiguration(IHomeConfiguration homeConfiguration)
        {
            this.homeConfiguration = homeConfiguration;
        }
        
        public string FileStorageDirectory
        {
            get { return EnsureExists(Path.Combine(homeConfiguration.HomeDirectory, "Files")); }
        }

        static string EnsureExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }
    }
}