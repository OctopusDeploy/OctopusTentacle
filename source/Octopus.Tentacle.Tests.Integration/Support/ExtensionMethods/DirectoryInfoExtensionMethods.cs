using System;
using System.IO;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    internal static class DirectoryInfoExtensionMethods
    {
        public static void CopyTo(this DirectoryInfo sourceDirectory, string destinationPath)
        {
            if (!sourceDirectory.Exists)
            {
                throw new ArgumentException($"{sourceDirectory.FullName} does not exist", nameof(sourceDirectory));
            }

            var destinationDirectory = new DirectoryInfo(destinationPath);
            if (!destinationDirectory.Exists)
            {
                destinationDirectory.Create();
            }

            var sourceFiles = sourceDirectory.GetFiles();
            foreach (var file in sourceFiles)
            {
                File.Copy(file.FullName, Path.Combine(destinationDirectory.FullName, file.Name), true);
            }

            var subDirectories = sourceDirectory.GetDirectories();
            foreach (var directory in subDirectories)
            {
                directory.CopyTo(Path.Combine(destinationDirectory.FullName, directory.Name));
            }
        }
    }
}
