using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Packages
{
    public class PackageSource : IPackageSource
    {
        readonly IPackageDownloader downloader;
        readonly List<NuGetFeedProperties> feeds;
        readonly PackageCachePolicy packageCachePolicy;
        readonly ConcurrentDictionary<PackageIdentifier, Lazy<StoredPackage>> storedPackages = new ConcurrentDictionary<PackageIdentifier, Lazy<StoredPackage>>();
        readonly ILog log = Log.Octopus();
        readonly LogContext logContext;

        public PackageSource(IPackageDownloader downloader, List<NuGetFeedProperties> feeds, PackageCachePolicy packageCachePolicy, LogContext logContext)
        {
            this.downloader = downloader;
            this.feeds = feeds;
            this.packageCachePolicy = packageCachePolicy;
            this.logContext = logContext;
        }

        public PackageCachePolicy PackageCachePolicy
        {
            get { return packageCachePolicy; }
        }

        public StoredPackage DownloadPackage(PackageIdentifier identifier)
        {
            return storedPackages.GetOrAdd(identifier, new Lazy<StoredPackage>(delegate
            {
                using (log.WithinBlock(logContext))
                {
                    return downloader.Download(new PackageMetadata(identifier.PackageId, identifier.Version), GetFeedProperties(identifier), packageCachePolicy);
                }
            })).Value;
        }
        
        public NuGetFeedProperties GetFeedProperties(PackageIdentifier identifier)
        {
            return feeds.Single(f => f.Id == identifier.FeedId);
        }
    }
}