using System;
using System.IO;

namespace Octopus.Shared.Packages
{
    public interface IPackageSource
    {
        PackageCachePolicy PackageCachePolicy { get; }
        StoredPackage DownloadPackage(PackageIdentifier identifier);
        NuGetFeedProperties GetFeedProperties(PackageIdentifier identifier);
    }
}