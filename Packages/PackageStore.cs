using System;
using System.IO;
using NuGet;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    public class PackageStore : IPackageStore
    {
        readonly IOctopusFileSystem fileSystem;
        readonly string rootDirectory;

        public PackageStore(IOctopusFileSystem fileSystem, string rootDirectory)
        {
            this.fileSystem = fileSystem;
            this.rootDirectory = rootDirectory;
        }

        public bool DoesPackageExist(PackageMetadata metadata)
        {
            var package = GetPackage(metadata);
            return package != null;
        }

        public Stream CreateFileForPackage(PackageMetadata metadata)
        {
            var prefix = GetPrefixForPackage(metadata);

            var fullPath = Path.Combine(rootDirectory, prefix + BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) + ".nupkg");

            fileSystem.EnsureDirectoryExists(rootDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(rootDirectory, metadata.Size);

            return fileSystem.OpenFile(fullPath, FileAccess.Write);
        }

        public StoredPackage GetPackage(PackageMetadata metadata)
        {
            var prefix = GetPrefixForPackage(metadata);
            fileSystem.EnsureDirectoryExists(rootDirectory);
            var files = fileSystem.EnumerateFiles(rootDirectory, prefix + "*.nupkg");

            foreach (var file in files)
            {
                var package = ReadPackageFile(file);
                if (package == null) 
                    continue;

                if (!string.Equals(package.PackageId, metadata.PackageId, StringComparison.OrdinalIgnoreCase) || !string.Equals(package.Version, metadata.Version, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(metadata.Hash))
                    return package;

                if (metadata.Hash == package.Hash)
                    return package;
            }

            return null;
        }

        StoredPackage ReadPackageFile(string filePath)
        {
            try
            {
                var metadata = new ZipPackage(filePath);
                string hash;

                var size = fileSystem.GetFileSize(filePath);
                
                using (var stream = fileSystem.OpenFile(filePath, FileAccess.Read, FileShare.ReadWrite))
                {
                    hash = HashCalculator.Hash(stream);
                }

                return new StoredPackage(metadata.Id, metadata.Version.ToString(), filePath, hash, size);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (FileFormatException)
            {
                return null;
            }
        }

        string GetPrefixForPackage(PackageMetadata metadata)
        {
            return metadata.PackageId + "." + metadata.Version + "_";
        }
    }
}