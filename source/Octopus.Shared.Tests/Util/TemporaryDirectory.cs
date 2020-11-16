using System;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    public class TemporaryDirectory : IDisposable
    {
        readonly IOctopusFileSystem fileSystem = new OctopusPhysicalFileSystem();

        public TemporaryDirectory(string directoryPath = null)
        {
            DirectoryPath = directoryPath ?? fileSystem.CreateTemporaryDirectory();
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            fileSystem.DeleteDirectory(DirectoryPath, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}