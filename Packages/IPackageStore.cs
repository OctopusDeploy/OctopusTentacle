using System;
using System.IO;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Packages
{
    public interface IPackageStore
    {
        bool DoesPackageExist(PackageMetadata metadata);
        Stream CreateFileForPackage(PackageMetadata metadata);
        StoredPackage GetPackage(PackageMetadata metadata);
    }
}