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
        readonly bool preferDownloadOnAgent;
        readonly PackageCachePolicy packageCachePolicy;
        readonly IEnumerable<string> targetRoles;

        public PackageAcquisition(string actionId, PackageMetadata package, string feedId, bool preferDownloadOnAgent, IEnumerable<string> targetRoles, PackageCachePolicy packageCachePolicy)
        {
            this.actionId = actionId;
            this.package = package;
            this.feedId = feedId;
            this.preferDownloadOnAgent = preferDownloadOnAgent;
            this.packageCachePolicy = packageCachePolicy;
            this.targetRoles = targetRoles.ToArray();
        }

        public PackageMetadata Package
        {
            get { return package; }
        }

        public IFeed Feed { get; set; }
        
        public string FeedName { get; set; }
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

        public string FeedId
        {
            get { return feedId; }
        }
    }
}
