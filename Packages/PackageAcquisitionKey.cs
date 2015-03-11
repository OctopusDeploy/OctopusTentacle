using System;

namespace Octopus.Shared.Packages
{
    public class PackageAcquisitionKey : IEquatable<PackageAcquisitionKey>
    {
        readonly string packageId;
        readonly string version;
        readonly string feedId;

        public PackageAcquisitionKey(string packageId, string version, string feedId)
        {
            this.packageId = packageId;
            this.version = version;
            this.feedId = feedId;
        }

        public string PackageId
        {
            get { return packageId; }
        }

        public string Version
        {
            get { return version; }
        }

        public string FeedId
        {
            get { return feedId; }
        }

        public bool Equals(PackageAcquisitionKey other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(packageId, other.packageId) && string.Equals(version, other.version) && string.Equals(feedId, other.feedId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((PackageAcquisitionKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (packageId != null ? packageId.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (version != null ? version.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (feedId != null ? feedId.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(PackageAcquisitionKey left, PackageAcquisitionKey right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PackageAcquisitionKey left, PackageAcquisitionKey right)
        {
            return !Equals(left, right);
        }
    }
}