﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Tests.FileSystem
{
    [TestFixture]
    public class FileSystemCleanerFixture
    {
        [Test]
        public void ShouldNotDeleteFilesThatWeNeed()
        {
            var fileSystem = new OctopusPhysicalFileSystem();

            var pathsThatWillBeDeleted = GetPathsThatWillBeDeleted();
            Assert.That(pathsThatWillBeDeleted, Is.Not.Empty);

            var filesThatShouldntBeDeleted = pathsThatWillBeDeleted.Where(p => fileSystem.FileExists(p));
            var directoriesThatShouldntBeDeleted = pathsThatWillBeDeleted.Where(p => fileSystem.DirectoryExists(p));

            OutputPathsThatShouldntBeDeleted(filesThatShouldntBeDeleted, "files");
            OutputPathsThatShouldntBeDeleted(directoriesThatShouldntBeDeleted, "directories");

            Assert.That(filesThatShouldntBeDeleted, Is.Empty);
            Assert.That(directoriesThatShouldntBeDeleted, Is.Empty);
        }

        static IReadOnlyList<string> GetPathsThatWillBeDeleted()
        {
            var assembly = typeof(FileSystemCleaner).Assembly;

            using (var stream = assembly.GetManifestResourceStream(FileSystemCleaner.PathsToDeleteOnStartupResource))
            using (var reader = new StreamReader(stream))
            {
                string line;
                var allLines = new List<string>();
                while ((line = reader.ReadLine()) != null)
                {
                    allLines.Add(line);
                }
                return allLines;
            }
        }

        static void OutputPathsThatShouldntBeDeleted(IEnumerable<string> paths, string pathType)
        {
            if (!paths.Any())
                return;

            Console.WriteLine($"The following {pathType} will be deleted on Octopus startup but we need them:");
            foreach (var p in paths)
                Console.WriteLine(p);
            Console.WriteLine("Remove the path from the list of files to be deleted in Octopus.Shared.");
        }
    }
}