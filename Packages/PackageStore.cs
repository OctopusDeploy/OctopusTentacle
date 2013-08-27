using System;
using System.IO;
using NuGet;
using Octopus.Platform.Util;
using Octopus.Shared.Contracts;

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
            return DoesPackageExist(null, metadata);
        }

        public bool DoesPackageExist(string prefix, PackageMetadata metadata)
        {
            var package = GetPackage(prefix, metadata);
            return package != null;
        }

        public Stream CreateFileForPackage(PackageMetadata metadata)
        {
            return CreateFileForPackage(null, metadata);
        }

        public Stream CreateFileForPackage(string prefix, PackageMetadata metadata)
        {
            var name = GetNameOfPackage(metadata);

            var fullPath = Path.Combine(GetPackageRoot(prefix), name + BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) + ".nupkg");

            fileSystem.EnsureDirectoryExists(rootDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(rootDirectory, metadata.Size);

            return fileSystem.OpenFile(fullPath, FileAccess.Write);
        }

        public string GetPackagesDirectory()
        {
            return GetPackageRoot(null);
        }

        public string GetPackagesDirectory(string prefix)
        {
            return GetPackageRoot(prefix);
        }

        public StoredPackage GetPackage(string packageFullPath)
        {
            return ReadPackageFile(packageFullPath);
        }

        public StoredPackage GetPackage(PackageMetadata metadata)
        {
            return GetPackage(null, metadata);
        }

        public StoredPackage GetPackage(string prefix, PackageMetadata metadata)
        {
            var name = GetNameOfPackage(metadata);
            var root = GetPackageRoot(prefix);
            fileSystem.EnsureDirectoryExists(root);

            var files = fileSystem.EnumerateFiles(root, name + "*.nupkg");

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

        string GetPackageRoot(string prefix)
        {
            return string.IsNullOrWhiteSpace(prefix) ? rootDirectory : Path.Combine(rootDirectory, prefix);
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

                var packageMetadata = new PackageMetadata(metadata.Id, metadata.Version.ToString(), size) {Hash = hash};

                return new StoredPackage(packageMetadata, filePath);
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

        static string GetNameOfPackage(PackageMetadata metadata)
        {
            return metadata.PackageId + "." + metadata.Version + "_";
        }
    }
}