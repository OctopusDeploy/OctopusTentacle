using System;
using System.IO;
using System.Threading;
using NuGet;
using Octopus.Shared.BuiltInFeed;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using SemanticVersion = Octopus.Client.Model.SemanticVersion;

namespace Octopus.Shared.Packages
{
    public class PackageDownloader : IPackageDownloader
    {
        const int NumberOfTimesToAttemptToDownloadPackage = 5;
        readonly IPackageStore packageStore;
        readonly IOctopusPackageRepositoryFactory packageRepositoryFactory;
        readonly IBuiltInPackageRepository builtInPackageRepository;
        readonly IOctopusFileSystem fileSystem;
        readonly ILog log;

        public PackageDownloader(
            IPackageStore packageStore,
            IOctopusPackageRepositoryFactory packageRepositoryFactory, 
            IBuiltInPackageRepository builtInPackageRepository,
            IOctopusFileSystem fileSystem,
            ILog log)
        {
            this.packageStore = packageStore;
            this.packageRepositoryFactory = packageRepositoryFactory;
            this.builtInPackageRepository = builtInPackageRepository;
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public StoredPackage Download(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy)
        {
            var storedPackage = builtInPackageRepository.IsBuiltInSource(feed.FeedUri) 
                ? GetPackageFromBuiltInRepository(package, builtInPackageRepository.CreateRepository()) 
                : DownloadFromExternalSource(package, feed, cachePolicy);

            log.Verbose("SHA1 hash of package " + storedPackage.PackageId + " is: " + storedPackage.Hash);

            return storedPackage;
        }

        StoredPackage DownloadFromExternalSource(PackageMetadata package, IFeed feed, PackageCachePolicy cachePolicy)
        {
            if (cachePolicy == PackageCachePolicy.UseCache)
            {
                var cached = AttemptToGetPackageFromCache(package, feed);
                if (cached != null)
                {
                    log.Verbose("Package "+ package.PackageId + " version " + package.Version + " was found in cache. No need to download. Using file: " + cached.FullPath);
                    return cached;
                }
            }

            return AttemptToDownload(package, feed);
        }

        StoredPackage GetPackageFromBuiltInRepository(PackageMetadata package, INuGetFeed feed)
        {
            log.Info("Looking up the package location from the built-in package repository...");

            var nugetPackage = feed.GetPackage(package.PackageId, new SemanticVersion(package.Version));
            if (nugetPackage == null) throw new ControlledFailureException(String.Format("The package {0} could not be located in the built-in repository.", package.PackageId));

            var hash = nugetPackage.CalculateHash().ToLowerInvariant();

            var path = builtInPackageRepository.GetFilePath(nugetPackage);
            return new StoredPackage(new PackageMetadata(nugetPackage.PackageId, nugetPackage.Version.ToString(), nugetPackage.GetSize(), hash), path);
        }

        StoredPackage AttemptToGetPackageFromCache(PackageMetadata metadata, IFeed feed)
        {
            log.VerboseFormat("Checking package cache for package {0} {1}", metadata.PackageId, metadata.Version);

            return packageStore.GetPackage(feed.Id, metadata);
        }

        StoredPackage AttemptToDownload(PackageMetadata metadata, IFeed feed)
        {
            log.InfoFormat("Downloading NuGet package {0} {1} from feed: '{2}'", metadata.PackageId, metadata.Version, feed.FeedUri);

            var cacheDirectory = packageStore.GetPackagesDirectory(feed.Id);
            log.VerboseFormat("Downloaded packages will be stored in: {0}", cacheDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            INuGetPackage downloaded = null;
            string downloadedTo = null;

            Exception downloadException = null;
            for (var i = 1; i <= NumberOfTimesToAttemptToDownloadPackage; i++)
            {
                try
                {
                    AttemptToFindAndDownloadPackage(i, metadata, feed, cacheDirectory, out downloaded, out downloadedTo);
                    break;
                }
                catch (Exception dataException)
                {
                    log.VerboseFormat(dataException, "Attempt {0} of {1}: Unable to download package: {2}", i, NumberOfTimesToAttemptToDownloadPackage, dataException.Message);
                    downloadException = dataException;
                    Thread.Sleep(i * 1000);
                }
            }

            if (downloaded == null || downloadedTo == null)
            {
                if (downloadException != null)
                    log.ErrorFormat(downloadException, "Unable to download package: ", downloadException.Message);

                throw new ControlledFailureException("The package could not be downloaded from NuGet. If you are getting a package verification error, try switching to a Windows File Share package repository to see if that helps.");
            }

            if (downloaded.Version.ToString() != metadata.Version)
                throw new ControlledFailureException(string.Format(
                    "Octopus requested version {0} of {1}, but the NuGet server returned a package with version {2}",
                    metadata.Version, metadata.PackageId, downloaded.Version));

            CheckWhetherThePackageHasDependencies(downloaded);

            var size = fileSystem.GetFileSize(downloadedTo);
            var hash = downloaded.CalculateHash();
            return new StoredPackage(metadata.PackageId, metadata.Version, downloadedTo, hash, size);
        }

        void CheckWhetherThePackageHasDependencies(INuGetPackage downloaded)
        {
            var dependencies = downloaded.GetDependencies();
            if (dependencies.Count > 0)
            {
                log.InfoFormat("NuGet packages with dependencies are not currently supported, and dependencies won't be installed on the Tentacle. The package '{0} {1}' appears to have the following dependencies: {2}. For more information please see {3}",
                               downloaded.PackageId,
                               downloaded.Version,
                               string.Join(", ", dependencies),
                               OutboundLinks.WhyAmINotAllowedToUseDependencies);
            }
        }

        void AttemptToFindAndDownloadPackage(int attempt, PackageMetadata packageMetadata, IFeed feed, string cacheDirectory, out INuGetPackage downloadedPackage, out string path)
        {
            NuGet.PackageDownloader downloader;
            var package = FindPackage(attempt, packageMetadata, feed, out downloader);

            var fullPathToDownloadTo = GetFilePathToDownloadPackageTo(cacheDirectory, package);

            DownloadPackage(package, fullPathToDownloadTo, downloader);

            path = fullPathToDownloadTo;
            downloadedPackage = new ExternalNuGetPackageAdapter(new ZipPackage(fullPathToDownloadTo));
        }

        INuGetPackage FindPackage(int attempt, PackageMetadata packageMetadata, IFeed feed, out NuGet.PackageDownloader downloader)
        {
            log.VerboseFormat("Finding package (attempt {0} of {1})", attempt, NumberOfTimesToAttemptToDownloadPackage);

            var remoteRepository = packageRepositoryFactory.CreateRepository(feed.FeedUri, feed.GetCredentials());

            var dspr = remoteRepository as DataServicePackageRepository;
            downloader = dspr != null ? dspr.PackageDownloader : null;

            var requiredVersion = new SemanticVersion(packageMetadata.Version);
            var package = remoteRepository.GetPackage(packageMetadata.PackageId, requiredVersion);

            if (package == null)
                throw new ControlledFailureException(string.Format("Could not find package {0} {1} in feed: '{2}'", packageMetadata.PackageId, packageMetadata.Version, feed.FeedUri));

            if (!requiredVersion.Equals(package.Version))
            {
                var message = string.Format("The package version '{0}' returned from the package repository doesn't match the requested package version '{1}'.", package.Version, requiredVersion);
                throw new ControlledFailureException(message);
            }

            return package;
        }

        void DownloadPackage(INuGetPackage nuGetPackage, string fullPathToDownloadTo, NuGet.PackageDownloader directDownloader)
        {
            var external = nuGetPackage as IExternalPackage;
            if (external == null)
                throw new Exception("Unexpected package: " + nuGetPackage.GetType());

            var package = external.GetRealPackage();

            log.VerboseFormat("Found package {0} version {1}", package.Id, package.Version);
            log.Verbose("Downloading to: " + fullPathToDownloadTo);

            var dsp = package as DataServicePackage;
            if(dsp != null && directDownloader != null)
            {
                log.Verbose("A direct download is possible; bypassing the NuGet machine cache");
                using (var targetFile = fileSystem.OpenFile(fullPathToDownloadTo, FileMode.CreateNew))
                    directDownloader.DownloadPackage(dsp.DownloadUrl, dsp, targetFile);
                return;
            }

            var physical = new PhysicalFileSystem(Path.GetDirectoryName(fullPathToDownloadTo));
            var local = new LocalPackageRepository(new FixedFilePathResolver(package.Id, fullPathToDownloadTo), physical);
            local.AddPackage(package);
        }

        static string GetFilePathToDownloadPackageTo(string cacheDirectory, INuGetPackage package)
        {
            var name = package.PackageId + "." + package.Version + "_" + BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) + Constants.PackageExtension;
            return Path.Combine(cacheDirectory, name);
        }
    }
}