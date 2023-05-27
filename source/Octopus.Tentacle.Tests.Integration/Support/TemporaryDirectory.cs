﻿using System;
using System.IO;
using System.Reflection;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TemporaryDirectory : IDisposable
    {
        readonly IOctopusFileSystem fileSystem;

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

            //var path = Path.GetTempPath();

            path = Path.Combine(path, Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly()!.GetName().Name! : "Octopus");
            //path = Path.Combine(path, "TentacleIT");
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