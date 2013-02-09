using System;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Packages
{
    public class StoredPackage : IEquatable<StoredPackage>
    {
        readonly PackageMetadata metadata;
        readonly string fullPath;
        
        public StoredPackage(PackageMetadata metadata, string fullPath)
        {
            this.metadata = metadata;
            this.fullPath = fullPath;
        }

        public StoredPackage(string packageId, string version, string fullPath, long length) 
            : this(packageId, version, fullPath, null, length)
        {
        }

        public StoredPackage(string packageId, string version, string fullPath, string hash, long length) 
            : this(new PackageMetadata(packageId, version, length) { Hash = hash }, fullPath)
        {
        }

        public string PackageId
        {
            get { return metadata.PackageId; }
        }

        public string Version
        {
            get { return metadata.Version; }
        }

        public string FullPath
        {
            get { return fullPath; }
        }

        public long Size
        {
            get { return metadata.Size; }
        }

        public string Hash
        {
            get { return metadata.Hash; }
        }

        public PackageMetadata Metadata
        {
            get { return metadata; }
        }

        public bool Equals(StoredPackage other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(metadata, other.metadata) && string.Equals(fullPath, other.fullPath);
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
                return ((metadata != null ? metadata.GetHashCode() : 0)*397) ^ (fullPath != null ? fullPath.GetHashCode() : 0);
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

        public static implicit operator PackageMetadata(StoredPackage package)
        {
            return package == null ? null : package.metadata;
        }

        public override string ToString()
        {
            return PackageId + "." + Version + " (" + (Size/1024.00/1024.00).ToString("n2") + " MB)";
        }
    }
}