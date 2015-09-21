using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Packages
{
    public class PackageAcquisition
    {
        readonly PackageMetadata package;
        readonly NuGetFeedProperties feed;
        readonly bool preferDownloadOnAgent;
        readonly bool deltaCompressionEnabled;
        readonly PackageCachePolicy packageCachePolicy;
        readonly IEnumerable<string> targetRoles;
        readonly PackageAcquisitionKey key;

        public PackageAcquisition(string actionId, PackageMetadata package, NuGetFeedProperties feed, bool preferDownloadOnAgent, bool deltaCompressionEnabled, IEnumerable<string> targetRoles, PackageCachePolicy packageCachePolicy)
        {
            ActionId = actionId;
            this.package = package;
            this.feed = feed;
            this.preferDownloadOnAgent = preferDownloadOnAgent;
            this.deltaCompressionEnabled = deltaCompressionEnabled;
            this.packageCachePolicy = packageCachePolicy;
            this.targetRoles = targetRoles.ToArray();
            key = new PackageAcquisitionKey(package.PackageId, package.Version, feed.Id);
        }

        public PackageMetadata Package
        {
            get { return package; }
        }

        public bool PreferDownloadOnAgent
        {
            get { return preferDownloadOnAgent; }
        }

        public bool DeltaCompressionEnabled
        {
            get {  return deltaCompressionEnabled; }
        }

        public IEnumerable<string> TargetRoles
        {
            get { return targetRoles; }
        }

        public string ActionId { get; private set; }

        public PackageCachePolicy PackageCachePolicy
        {
            get { return packageCachePolicy; }
        }

        public NuGetFeedProperties Feed
        {
            get { return feed; }
        }

        public PackageAcquisitionKey GetUniqueSourcePackageKey()
        {
            return key;
        }
    }
}