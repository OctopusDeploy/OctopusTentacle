using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NuGet;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    public interface IPackageDownloader
    {
        StoredPackage Download(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy, IActivityLog log);
    }

    public enum PackageCachePolicy
    {
        UseCache,
        BypassCache
    }

    public class PackageDownloader : IPackageDownloader
    {
        const int NumberOfTimesToAttemptToDownloadPackage = 5;
        readonly IPackageStore packageStore;
        readonly IPackageRepositoryFactory packageRepositoryFactory;
        readonly IOctopusFileSystem fileSystem;
        
        public PackageDownloader(IPackageStore packageStore, IPackageRepositoryFactory packageRepositoryFactory, IOctopusFileSystem fileSystem)
        {
            this.packageStore = packageStore;
            this.packageRepositoryFactory = packageRepositoryFactory;
            this.fileSystem = fileSystem;
        }

        public StoredPackage Download(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy, IActivityLog log)
        {
            StoredPackage storedPackage = null;

            if (cachePolicy == PackageCachePolicy.UseCache)
            {
                storedPackage = AttemptToGetPackageFromCache(package, feed, log);
            }

            if (storedPackage == null)
            {
                storedPackage = AttemptToDownload(package, feed, log);
            }
            else
            {
                log.Debug("Package was found in cache. No need to download. Using file: " + storedPackage.FullPath);
            }

            log.Debug("SHA1 hash of package is: " + storedPackage.Hash);

            return storedPackage;
        }

        StoredPackage AttemptToGetPackageFromCache(PackageMetadata metadata, IFeed feed, IActivityLog log)
        {
            log.DebugFormat("Checking package cache for package {0} {1}", metadata.PackageId, metadata.Version);

            return packageStore.GetPackage(feed.Id, metadata);
        }

        StoredPackage AttemptToDownload(PackageMetadata metadata, IFeed feed, IActivityLog log)
        {
            log.InfoFormat("Downloading NuGet package {0} {1} from feed: '{2}'", metadata.PackageId, metadata.Version, feed.FeedUri);

            var cacheDirectory = packageStore.GetPackagesDirectory(feed.Id);
            log.DebugFormat("Downloaded packages will be stored in: {0}", cacheDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            IPackage downloaded = null;
            string downloadedTo = null;

            for (var i = 1; i <= NumberOfTimesToAttemptToDownloadPackage; i++)
            {
                try
                {
                    AttemptToFindAndDownloadPackage(i, metadata, feed, log, cacheDirectory, out downloaded, out downloadedTo);
                    break;
                }
                catch (Exception dataException)
                {
                    log.Error("Unable to download package: " + dataException.Message, dataException);
                    Thread.Sleep(i * 1000);
                }
            }

            if (downloaded == null || downloadedTo == null)
            {
                throw new Exception("The package could not be downloaded from NuGet. Please see the below errors for details. If you are getting a package verification error, try switching to a Windows File Share package repository to see if that helps.");
            }

            CheckWhetherThePackageHasDependencies(downloaded, log);

            var size = fileSystem.GetFileSize(downloadedTo);
            var hash = HashCalculator.Hash(downloaded.GetStream());
            return new StoredPackage(metadata.PackageId, metadata.Version, downloadedTo, hash, size);
        }

        static void CheckWhetherThePackageHasDependencies(IPackageMetadata downloaded, IActivityLog log)
        {
            var dependencies = downloaded.DependencySets.SelectMany(ds => ds.Dependencies).Count();
            if (dependencies > 0)
            {
                log.WarnFormat("NuGet packages with dependencies are not currently supported, and dependencies won't be installed on the Tentacle. The package '{0} {1}' appears to have the following dependencies: {2}. For more information please see {3}",
                               downloaded.Id,
                               downloaded.Version,
                               string.Join(", ", downloaded.DependencySets.SelectMany(ds => ds.Dependencies).Select(dependency => dependency.ToString())),
                               OutboundLinks.WhyAmINotAllowedToUseDependencies);
            }
        }

        void AttemptToFindAndDownloadPackage(int attempt, PackageMetadata packageMetadata, IFeed feed, IActivityLog log, string cacheDirectory, out IPackage downloadedPackage, out string path)
        {
            var package = FindPackage(attempt, packageMetadata, feed, log);

            var fullPathToDownloadTo = GetFilePathToDownloadPackageTo(cacheDirectory, package);

            DownloadPackage(package, feed, fullPathToDownloadTo, log);

            path = fullPathToDownloadTo;
            downloadedPackage = new ZipPackage(fullPathToDownloadTo);
        }

        IPackage FindPackage(int attempt, PackageMetadata packageMetadata, IFeed feed, IActivityLog log)
        {
            log.DebugFormat("Finding package (attempt {0} of {1})", attempt, NumberOfTimesToAttemptToDownloadPackage);

            var remoteRepository = packageRepositoryFactory.CreateRepository(feed.FeedUri, feed.GetCredentials());
            var package = remoteRepository.FindPackage(packageMetadata.PackageId, new SemanticVersion(packageMetadata.Version), true, true);

            if (package == null)
                throw new Exception(String.Format("Could not find package {0} {1} in feed: '{2}'", packageMetadata.PackageId, packageMetadata.Version, feed.FeedUri));

            return package;
        }

        static void DownloadPackage(IPackage package, IFeed feed, string fullPathToDownloadTo, IActivityLog log)
        {
            log.DebugFormat("Found package {0} version {1}", package.Id, package.Version);
            log.Debug("Downloading to: " + fullPathToDownloadTo);

            var physical = new PhysicalFileSystem(Path.GetDirectoryName(fullPathToDownloadTo));
            var local = new LocalPackageRepository(new FixedFilePathResolver(package.Id, fullPathToDownloadTo), physical);
            local.AddPackage(package);
        }

        static string GetFilePathToDownloadPackageTo(string cacheDirectory, IPackageMetadata package)
        {
            var name = package.Id + "." + package.Version + "_" + BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) + Constants.PackageExtension;
            return Path.Combine(cacheDirectory, name);
        }
    }
}