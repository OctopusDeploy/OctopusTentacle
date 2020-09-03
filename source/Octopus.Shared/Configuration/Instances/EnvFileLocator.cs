using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IEnvFileLocator
    {
        string? LocateEnvFile();
    }
    
    public class EnvFileLocator : IEnvFileLocator
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ILog log;

        public EnvFileLocator(IOctopusFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public string? LocateEnvFile()
        {
            var directoryToCheck = Path.GetDirectoryName(typeof(InMemoryKeyValueStore).Assembly.Location);

            if (log != null)
            {
                log.InfoFormat("Search for .env file, starting from {0}", directoryToCheck);
            }

            var envPathToCheck = Path.Combine(directoryToCheck, ".env");
            var envFileExists = fileSystem.FileExists(envPathToCheck);
            var rootDirectoryReached = false;

            while (!envFileExists && !rootDirectoryReached)
            {
                var lastPathSeparator = directoryToCheck.LastIndexOf(Path.DirectorySeparatorChar);
                directoryToCheck = directoryToCheck.Substring(0, lastPathSeparator);

                if (lastPathSeparator >= 0 && lastPathSeparator <= 2)
                    rootDirectoryReached = true;

                if (rootDirectoryReached)
                    directoryToCheck += Path.DirectorySeparatorChar; // for root path when need to tack the separator back on
                
                envPathToCheck = Path.Combine(directoryToCheck, ".env");
                
                envFileExists = fileSystem.FileExists(envPathToCheck);
            }

            if (log != null)
                if (envFileExists)
                    log.InfoFormat("Found .env file, {0}", envPathToCheck);
                else
                    log.Info("No .env file found");

            return envFileExists ? envPathToCheck : null;
        }
    }
}