using System;
using System.IO;
using System.Reflection;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TemporaryDirectory : IDisposable
    {
        readonly IOctopusFileSystem fileSystem;
        private bool deleted;

        public TemporaryDirectory(IOctopusFileSystem fileSystem, string? directoryPath = null)
        {
            this.fileSystem = fileSystem;
            DirectoryPath = directoryPath ?? CreateTemporaryDirectory();
        }

        public TemporaryDirectory() : this(new OctopusPhysicalFileSystem(new InMemoryLog()))
        {
        }

        public string DirectoryPath { get; }

        string GetTempBasePath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            fileSystem.EnsureDirectoryExists(path);

            path = Path.Combine(path, Assembly.GetEntryAssembly()?.GetName()?.Name ?? "Octopus");
            return Path.Combine(path, "Temp");
        }

        string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        public (bool deleted, Exception? deleteException) TryDelete()
        {
            if (!deleted)
            {
                if (Directory.Exists(DirectoryPath))
                {
                    try
                    {
                        Directory.Delete(DirectoryPath, true);
                    }
                    catch (Exception e)
                    {
                        return (false, e);
                    }
                }

                deleted = true;
            }

            return (true, null);
        }

        public void Dispose()
        {
            TryDelete();
        }
    }
}