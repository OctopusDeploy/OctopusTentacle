using System;
using System.IO;
using NuGet;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    public class PackageMetadata : IEquatable<PackageMetadata>
    {
        public PackageMetadata()
        {
        }

        public PackageMetadata(string packageId, string version) : this(packageId, version, 0)
        {
        }

        public PackageMetadata(string packageId, string version, long size) : this(packageId, version, size, null)
        {
        }

        public PackageMetadata(string packageId, string version, long size, string hash)
        {
            PackageId = packageId;
            Version = version;
            Size = size;
            Hash = hash;
        }

        public string PackageId { get; set; }
        public string Version { get; set; }
        public long Size { get; set; }
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
            if (obj.GetType() != GetType()) return false;
            return Equals((PackageMetadata)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (PackageId != null ? PackageId.GetHashCode() : 0);
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

            string hash;
            using (var fileStream = File.OpenRead(filePath))
            {
                hash = HashCalculator.Hash(fileStream);
            }

            return new PackageMetadata(id, version, file.Length) {Hash = hash};
        }

        public override string ToString()
        {
            return string.Format("{0} version {1}", PackageId, Version);
        }
    }
}