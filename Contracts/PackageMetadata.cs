using System;
using System.IO;
using System.Runtime.Serialization;
using NuGet;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class PackageMetadata : IEquatable<PackageMetadata>
    {
        public PackageMetadata()
        {
        }

        public PackageMetadata(string packageId, string version) : this(packageId, version, 0)
        {
        }

        public PackageMetadata(string packageId, string version, long size)
        {
            PackageId = packageId;
            Version = version;
            Size = size;
        }

        [DataMember]
        public string PackageId { get; set; }

        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public long Size { get; set; }

        [DataMember]
        public string Hash { get; set; }

        public bool Equals(PackageMetadata other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(PackageId, other.PackageId) && string.Equals(Version, other.Version) && Size == other.Size && string.Equals(Hash, other.Hash);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PackageMetadata) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (PackageId != null ? PackageId.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Version != null ? Version.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Size.GetHashCode();
                hashCode = (hashCode*397) ^ (Hash != null ? Hash.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(PackageMetadata left, PackageMetadata right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PackageMetadata left, PackageMetadata right)
        {
            return !Equals(left, right);
        }

        public static PackageMetadata FromFile(string filePath)
        {
            var file = new FileInfo(filePath);
            if (!file.Exists)
            {
                throw new FileNotFoundException(string.Format("Could not find NuGet package file '{0}'", filePath), filePath);
            }

            var package = new ZipPackage(filePath);
            var id = package.Id;
            var version = package.Version.ToString();

            return new PackageMetadata(id, version, file.Length);
        }
    }
}