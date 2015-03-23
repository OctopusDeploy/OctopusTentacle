using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Packages
{
    public class PackageAcquisition
    {
        readonly string actionId;
        readonly PackageMetadata package;
        readonly string feedId;
        readonly NuGetFeedProperties feed;
        readonly bool preferDownloadOnAgent;
        readonly PackageCachePolicy packageCachePolicy;
        readonly IEnumerable<string> targetRoles;
        PackageAcquisitionKey key;

        public PackageAcquisition(string actionId, PackageMetadata package, NuGetFeedProperties feed, bool preferDownloadOnAgent, IEnumerable<string> targetRoles, PackageCachePolicy packageCachePolicy)
        {
            this.actionId = actionId;
            this.package = package;
            this.feed = feed;
            this.preferDownloadOnAgent = preferDownloadOnAgent;
            this.packageCachePolicy = packageCachePolicy;
            this.targetRoles = targetRoles.ToArray();
            key = new PackageAcquisitionKey(package.PackageId, package.Version, feedId);
        }

        public PackageMetadata Package
        {
            get { return package; }
        }

        public bool PreferDownloadOnAgent
        {
            get { return preferDownloadOnAgent; }
        }

        public IEnumerable<string> TargetRoles
        {
            get { return targetRoles; }
        }

        public string ActionId
        {
            get { return actionId; }
        }

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
