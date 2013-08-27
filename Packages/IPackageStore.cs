using System;
using System.IO;
using Octopus.Platform.Deployment.Packages;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Packages
{
    public interface IPackageStore
    {
        bool DoesPackageExist(PackageMetadata metadata);
        bool DoesPackageExist(string prefix, PackageMetadata metadata);

        Stream CreateFileForPackage(PackageMetadata metadata);
        Stream CreateFileForPackage(string prefix, PackageMetadata metadata);

        string GetPackagesDirectory();
        string GetPackagesDirectory(string prefix);

        StoredPackage GetPackage(string packageFullPath);
        StoredPackage GetPackage(PackageMetadata metadata);
        StoredPackage GetPackage(string prefix, PackageMetadata metadata);
    }
}