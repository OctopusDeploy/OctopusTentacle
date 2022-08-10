using System;
using System.Collections.Generic;
using System.IO;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Startup
{
    [TestFixture]
    public class FileSystemCleanerFixture
    {
        [Test]
        public void ShouldDeleteDirectories()
        {
            var paths = LoadPaths();
            var log = new InMemoryLog();

            foreach (var path in paths)
            {
                var fileSystem = Substitute.For<IOctopusFileSystem>();
                fileSystem.DirectoryExists(Arg.Is(path)).Returns(true);

                var target = new FileSystemCleaner(fileSystem, log);
                target.Clean(FileSystemCleaner.PathsToDeleteOnStartupResource);

                fileSystem.Received(1).DeleteDirectory(path, DeletionOptions.TryThreeTimes);
            }
        }

        [Test]
        public void ShouldDeleteFiles()
        {
            var paths = LoadPaths();
            var log = new InMemoryLog();

            foreach (var path in paths)
            {
                var fileSystem = Substitute.For<IOctopusFileSystem>();
                fileSystem.FileExists(Arg.Is(path)).Returns(true);

                var target = new FileSystemCleaner(fileSystem, log);
                target.Clean(FileSystemCleaner.PathsToDeleteOnStartupResource);

                fileSystem.Received(1).DeleteFile(path, DeletionOptions.TryThreeTimes);
            }
        }

        static IEnumerable<string> LoadPaths()
        {
            var assembly = typeof(FileSystemCleaner).Assembly;
            var root = Path.GetDirectoryName(assembly.FullLocalPath());

            using (var stream = assembly.GetManifestResourceStream(FileSystemCleaner.PathsToDeleteOnStartupResource))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    var path = Path.Combine(root, line);
                    yield return path;
                }
            }
        }
    }
}