using System;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    public class TemporaryDirectory : IDisposable
    {
        readonly IOctopusFileSystem fileSystem;

        public TemporaryDirectory(IOctopusFileSystem fileSystem, string directoryPath = null)
        {
            this.fileSystem = fileSystem;
            DirectoryPath = directoryPath ?? fileSystem.CreateTemporaryDirectory();
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            fileSystem.DeleteDirectory(DirectoryPath, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}