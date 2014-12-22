using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Packages
{
    public interface IPackageDownloader
    {
        StoredPackage Download(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy, IActivity activity);
    }
}