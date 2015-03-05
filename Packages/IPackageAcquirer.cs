using System;
using System.IO;

namespace Octopus.Shared.Packages
{
    public interface IPackageAcquirer
    {
        Stream Download(PackageMetadata package, IFeed feed, PackageCachePolicy packageCachePolicy);
    }
}
