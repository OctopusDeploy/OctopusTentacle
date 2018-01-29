using System;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    public class TemporaryDirectory : IDisposable
    {
        public string DirectoryPath { get; }

        readonly IOctopusFileSystem fileSystem = new OctopusPhysicalFileSystem();

        public TemporaryDirectory(string directoryPath = null)
        {
            DirectoryPath = directoryPath ?? fileSystem.CreateTemporaryDirectory();
        }

        public void Dispose()
        {
            fileSystem.DeleteDirectory(DirectoryPath, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}