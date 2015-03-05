using System;
using System.IO;
using Octopus.Client.Model;
using Octopus.Shared.BuiltInFeed;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Packages
{
    public class PackageAcquirer : IPackageAcquirer
    {
        readonly ILog log = Log.Octopus();
        readonly IBuiltInPackageRepository packageRepository;
        readonly IPackageDownloader packageDownloader;
        readonly INuGetFeed nugetFeed;

        public PackageAcquirer(IBuiltInPackageRepository packageRepository, IPackageDownloader packageDownloader)
        {
            this.packageRepository = packageRepository;
            this.packageDownloader = packageDownloader;
            nugetFeed = packageRepository.CreateRepository();
        }

        public Stream Download(PackageMetadata package, IFeed feed,PackageCachePolicy packageCachePolicy)
        {
            return packageRepository.IsBuiltInSource(feed.FeedUri)
                ? GetPackageFromBuiltInFeed(package)
                : GetPackageFromExternalFeed(feed, package, packageCachePolicy);
        }

        Stream GetPackageFromBuiltInFeed(PackageMetadata package)
        {
            log.Info("Looking up the package location from the built-in package repository...");

            var nugetPackage = nugetFeed.GetPackage(package.PackageId, new SemanticVersion(package.Version));
            if (nugetPackage == null) throw new ControlledFailureException(String.Format("The package {0} could not be located in the built-in repository.", package.PackageId));

            var hash = nugetPackage.CalculateHash().ToLowerInvariant();
            log.VerboseFormat("SHA1 hash of package is: {0}", hash);

            return nugetFeed.GetPackageRaw(nugetPackage.PackageId, nugetPackage.Version);
        }

        Stream GetPackageFromExternalFeed(IFeed feed, PackageMetadata package, PackageCachePolicy packageCachePolicy)
        {
            var downloadedPackage = packageDownloader.Download(package, feed, packageCachePolicy);

            return new FileStream(downloadedPackage.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
