using System;
using System.IO;

namespace Octopus.Shared.Packages
{
    public class PackageToAcquire
    {
        readonly Lazy<StoredPackage> onlyDownloadPackageOnce;

        public PackageToAcquire(IPackageDownloader downloader, PackageMetadata package, IFeed feed, PackageCachePolicy packageCachePolicy)
        {
            onlyDownloadPackageOnce = new Lazy<StoredPackage>(() => downloader.Download(package, feed, packageCachePolicy));
        }

        public Stream Download()
        {
            return new FileStream(onlyDownloadPackageOnce.Value.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
