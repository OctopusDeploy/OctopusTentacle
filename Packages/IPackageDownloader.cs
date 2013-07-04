using System;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Packages
{
    public interface IPackageDownloader
    {
        StoredPackage Download(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy, IActivityLog log);
    }
}