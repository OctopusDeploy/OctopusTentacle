using System;
using System.IO;
using System.Reflection;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    class TemporaryDirectory : IDisposable
    {
        readonly IOctopusFileSystem fileSystem;

        public TemporaryDirectory(IOctopusFileSystem fileSystem, string? directoryPath = null)
        {
            this.fileSystem = fileSystem;
            DirectoryPath = directoryPath ?? CreateTemporaryDirectory();
        }

        public string DirectoryPath { get; }

        string GetTempBasePath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            fileSystem.EnsureDirectoryExists(path);

            path = Path.Combine(path, Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly()!.GetName().Name! : "Octopus");
            return Path.Combine(path, "Temp");
        }

        string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }
        public void Dispose()
        {
            fileSystem.DeleteDirectory(DirectoryPath, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}