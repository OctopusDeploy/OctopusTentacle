using System;
using System.IO;

namespace Octopus.Shared.Packages
{
    public interface IPackageStore
    {
        bool DoesPackageExist(PackageMetadata metadata);
        bool DoesPackageExist(string prefix, PackageMetadata metadata);
        string GetFilenameForPackage(PackageMetadata package, string prefix = null);
        Stream CreateFileForPackage(PackageMetadata metadata, string prefix = null);
        string GetPackagesDirectory();
        string GetPackagesDirectory(string prefix);
        StoredPackage GetPackage(string packageFullPath);
        StoredPackage GetPackage(PackageMetadata metadata);
        StoredPackage GetPackage(string prefix, PackageMetadata metadata);
    }
}