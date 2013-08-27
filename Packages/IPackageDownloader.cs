using System;
using Octopus.Platform.Packages;
using Octopus.Shared.Contracts;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Packages
{
    public interface IPackageDownloader
    {
        StoredPackage Download(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy, IActivity activity);
    }
}