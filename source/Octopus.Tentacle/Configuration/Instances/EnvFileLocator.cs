using System;
using System.IO;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Instances
{
    public interface IEnvFileLocator
    {
        string? LocateEnvFile();
    }

    public class EnvFileLocator : IEnvFileLocator
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ILogFileOnlyLogger log;
        string? envFile;

        public EnvFileLocator(IOctopusFileSystem fileSystem, ILogFileOnlyLogger log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public string? LocateEnvFile()
        {
            if (envFile != null)
                return envFile;

            var directoryToCheck = Path.GetDirectoryName(typeof(InMemoryKeyValueStore).Assembly.Location) ?? throw new Exception("Could not get assembly location");

            log.Info($"Search for .env file, starting from {directoryToCheck}");

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

            if (envFileExists)
                log.Info($"Found .env file, {envPathToCheck}");
            else
                log.Info("No .env file found");

            envFile = envFileExists ? envPathToCheck : null;
            return envFile;
        }
    }
}