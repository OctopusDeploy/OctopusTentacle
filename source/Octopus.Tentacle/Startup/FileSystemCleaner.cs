using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Startup
{
    public class FileSystemCleaner
    {
        private readonly IOctopusFileSystem fileSystem;
        private readonly ISystemLog log;
#if NETFX
        public const string PathsToDeleteOnStartupResource = "Octopus.Tentacle.Startup.PathsToDeleteOnStartup.netfx.txt";
#else
        public const string PathsToDeleteOnStartupResource = "Octopus.Tentacle.Startup.PathsToDeleteOnStartup.core.txt";
#endif

        public FileSystemCleaner(IOctopusFileSystem fileSystem, ISystemLog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public void Clean(string resource)
        {
            using (var stream = LoadStreamFromAssembly(resource))
            {
                AttemptToDeleteEachEntryInStream(stream);
            }
        }

        private static Stream LoadStreamFromAssembly(string resource)
        {
            var assembly = typeof(FileSystemCleaner).Assembly;

            return assembly.GetManifestResourceStream(resource) ?? throw new Exception($"Resource {resource} not found");
        }

        private void AttemptToDeleteEachEntryInStream(Stream stream)
        {
            var root = Path.GetDirectoryName(typeof(FileSystemCleaner).Assembly.FullLocalPath()) ?? throw new Exception("Could not get directory");
            using (var reader = new StreamReader(stream))
            {
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    var path = Path.Combine(root, line);
                    try
                    {
                        DeleteFileOrDirectory(path);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Failed to delete: {path}");
                        log.Warn(ex.PrettyPrint());
                    }
                }
            }
        }

        private void DeleteFileOrDirectory(string path)
        {
            if (fileSystem.DirectoryExists(path))
            {
                log.Info($"Deleting the directory: {path}");
                fileSystem.DeleteDirectory(path, DeletionOptions.TryThreeTimes);
            }
            else if (fileSystem.FileExists(path))
            {
                log.Info($"Deleting the file: {path}");
                fileSystem.DeleteFile(path, DeletionOptions.TryThreeTimes);
            }
        }
    }
}