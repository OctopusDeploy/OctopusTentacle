using System;
using System.IO;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Packages
{
    public class PackageToAcquire
    {
        readonly PackageAcquisitionKey packageAcquisitionKey;
        readonly Lazy<StoredPackage> onlyDownloadPackageOnce;
        readonly ILog log = Log.Octopus();
        readonly LogCorrelator logCorrelator;

        public PackageToAcquire(IPackageDownloader downloader, PackageMetadata package, IFeed feed, PackageCachePolicy packageCachePolicy, PackageAcquisitionKey packageAcquisitionKey)
        {
            this.packageAcquisitionKey = packageAcquisitionKey;
            logCorrelator = log.Current;

            onlyDownloadPackageOnce = new Lazy<StoredPackage>(() =>
            {
                using (log.WithinBlock(logCorrelator))
                return downloader.Download(package, feed, packageCachePolicy);
            });
        }

        public PackageAcquisitionKey PackageAcquisitionKey
        {
            get { return packageAcquisitionKey; }
        }

        public StoredPackage Package
        {
            get { return onlyDownloadPackageOnce.Value; }
        }

        public string Hash
        {
            get { return onlyDownloadPackageOnce.Value.Hash; }
        }

        public Stream Download()
        {
            return new FileStream(onlyDownloadPackageOnce.Value.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}