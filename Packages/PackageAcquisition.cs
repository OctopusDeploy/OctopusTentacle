using System;
using System.Collections.Generic;
using System.Linq;
using Octostache;

namespace Octopus.Shared.Packages
{
    public class PackageAcquisition
    {
        readonly PackageMetadata package;
        readonly NuGetFeedProperties feed;
        readonly bool preferDownloadOnAgent;
        readonly PackageCachePolicy packageCachePolicy;
        readonly IEnumerable<string> targetRoles;
        readonly PackageAcquisitionKey key;

        public PackageAcquisition(string actionId, PackageMetadata package, NuGetFeedProperties feed, bool preferDownloadOnAgent, IEnumerable<string> targetRoles, PackageCachePolicy packageCachePolicy, VariableDictionary variables )
        {
            ActionId = actionId;
            Variables = variables;
            this.package = package;
            this.feed = feed;
            this.preferDownloadOnAgent = preferDownloadOnAgent;
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

        public VariableDictionary Variables { get; private set; }
    }
}
