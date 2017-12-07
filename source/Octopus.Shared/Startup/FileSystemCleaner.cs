using System;
using System.IO;
using Octopus.Shared.Util;
using Octopus.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class FileSystemCleaner
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ILog log;

        public const string PathsToDeleteOnStartupResource = "Octopus.Shared.Startup.PathsToDeleteOnStartup.txt";

        public FileSystemCleaner(IOctopusFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public void Clean(string resource)
        {
            using (var stream = LoadStreamFromAssembly(resource))
                AttemptToDeleteEachEntryInStream(stream);
        }

        static Stream LoadStreamFromAssembly(string resource)
        {
            var assembly = typeof(FileSystemCleaner).Assembly;

            return assembly.GetManifestResourceStream(resource);
        }

        void AttemptToDeleteEachEntryInStream(Stream stream)
        {
            var root = Path.GetDirectoryName(typeof(FileSystemCleaner).Assembly.FullLocalPath());
            using (var reader = new StreamReader(stream))
            {
                string line;

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

        void DeleteFileOrDirectory(string path)
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