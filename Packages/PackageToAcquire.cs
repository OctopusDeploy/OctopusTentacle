using System;
using System.IO;

namespace Octopus.Shared.Packages
{
    public class PackageToAcquire
    {
        readonly PackageAcquisitionKey packageAcquisitionKey;
        readonly Lazy<StoredPackage> onlyDownloadPackageOnce;

        public PackageToAcquire(IPackageDownloader downloader, PackageMetadata package, IFeed feed, PackageCachePolicy packageCachePolicy, PackageAcquisitionKey packageAcquisitionKey)
        {
            this.packageAcquisitionKey = packageAcquisitionKey;
            onlyDownloadPackageOnce = new Lazy<StoredPackage>(() => downloader.Download(package, feed, packageCachePolicy));
        }

        public PackageAcquisitionKey PackageAcquisitionKey
        {
            get { return packageAcquisitionKey; }
        }

        public StoredPackage Package { get { return onlyDownloadPackageOnce.Value; } } 

        public Stream Download()
        {
            return new FileStream(onlyDownloadPackageOnce.Value.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
