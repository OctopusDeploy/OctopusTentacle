using System;
using System.Collections.Generic;
using System.IO;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Startup;
using Octopus.Shared.Tests.Support;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Startup
{
    [TestFixture]
    public class FileSystemCleanerFixture
    {
        readonly Random random = new Random();

        [Test]
        public void ShouldDeleteFilesAndDirectories()
        {
            var paths = LoadPaths();
            string directoryPath;
            string filePath;
            do
            {
                directoryPath = GetRandomPath(paths);
                filePath = GetRandomPath(paths);
            } while (directoryPath == filePath);
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.FileExists(Arg.Is(filePath)).Returns(true);
            fileSystem.DirectoryExists(Arg.Is(directoryPath)).Returns(true);

            var log = new InMemoryLog();

            var target = new FileSystemCleaner(fileSystem, log);

            target.Clean(FileSystemCleaner.PathsToDeleteOnStartupResource);

            fileSystem.Received(1).DeleteFile(filePath, DeletionOptions.TryThreeTimes);
            fileSystem.Received(1).DeleteDirectory(directoryPath, DeletionOptions.TryThreeTimes);
        }

        string GetRandomPath(IReadOnlyList<string> paths)
        {
            return paths[random.Next(paths.Count)];
        }

        static List<string> LoadPaths()
        {
            var assembly = typeof(FileSystemCleaner).Assembly;
            var root = Path.GetDirectoryName(assembly.FullLocalPath());

            using (var stream = assembly.GetManifestResourceStream(FileSystemCleaner.PathsToDeleteOnStartupResource))
            using (var reader = new StreamReader(stream))
            {
                string line;
                var paths = new List<string>();
                while ((line = reader.ReadLine()) != null)
                {
                    var path = Path.Combine(root, line);
                    paths.Add(path);
                }
                return paths;
            }
        }
    }
}