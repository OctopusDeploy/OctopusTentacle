using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Packages
{
    public class RequiredPackage
    {
        readonly PackageMetadata packageMetadata;
        readonly string feedId;
        readonly bool preferDownloadOnAgent;
        readonly IEnumerable<string> targetRoles;
        readonly PackageIdentifier key;

        public RequiredPackage(string actionId, PackageMetadata packageMetadata, string feedId, bool preferDownloadOnAgent, IEnumerable<string> targetRoles)
        {
            ActionId = actionId;
            this.packageMetadata = packageMetadata;
            this.feedId = feedId;
            this.preferDownloadOnAgent = preferDownloadOnAgent;
            this.targetRoles = targetRoles.ToArray();
            key = new PackageIdentifier(packageMetadata.PackageId, packageMetadata.Version, feedId);
        }

        public PackageMetadata PackageMetadata
        {
            get { return packageMetadata; }
        }

        public string FeedId
        {
            get { return feedId; }
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

        public PackageIdentifier GetIdentifier()
        {
            return key;
        }
    }
}