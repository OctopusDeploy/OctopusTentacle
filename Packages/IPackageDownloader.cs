using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Packages;
using Octopus.Platform.Packages;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Packages
{
    public interface IPackageDownloader
    {
        StoredPackage Download(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy, IActivity activity);
    }
}