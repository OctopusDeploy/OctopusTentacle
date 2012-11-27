using System;
using System.IO;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Packages
{
    public interface IPackageStore
    {
        bool DoesPackageExist(PackageMetadata metadata);
        bool DoesPackageExist(string prefix, PackageMetadata metadata);

        Stream CreateFileForPackage(PackageMetadata metadata);
        Stream CreateFileForPackage(string prefix, PackageMetadata metadata);

        StoredPackage GetPackage(PackageMetadata metadata);
        StoredPackage GetPackage(string prefix, PackageMetadata metadata);
    }
}