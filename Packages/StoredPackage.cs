using System;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Packages
{
    public class StoredPackage : IEquatable<StoredPackage>
    {
        readonly string packageId;
        readonly string version;
        readonly string fullPath;
        readonly string hash;
        readonly long size;

        public StoredPackage(string packageId, string version, string fullPath, long size) : this(packageId, version, fullPath, string.Empty, size)
        {
        }

        public StoredPackage(string packageId, string version, string fullPath, string hash, long size)
        {
            this.packageId = packageId;
            this.version = version;
            this.fullPath = fullPath;
            this.hash = hash;
            this.size = size;
        }

        public string PackageId
        {
            get { return packageId; }
        }

        public string Version
        {
            get { return version; }
        }

        public string FullPath
        {
            get { return fullPath; }
        }

        public long Size
        {
            get { return size; }
        }

        public string Hash
        {
            get { return hash; }
        }

        public PackageMetadata GetMetadata()
        {
            return new PackageMetadata(packageId, version, size) { Hash = hash };
        }

        public bool Equals(StoredPackage other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(packageId, other.packageId) && string.Equals(version, other.version) && string.Equals(hash, other.hash);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StoredPackage) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (packageId != null ? packageId.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (version != null ? version.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (hash != null ? hash.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(StoredPackage left, StoredPackage right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StoredPackage left, StoredPackage right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return PackageId + "." + Version + " (" + (Size/1024.00/1024.00).ToString("n2") + " MB)";
        }
    }
}