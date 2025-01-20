using System;
using System.Text.RegularExpressions;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public class ClusterVersion : IComparable<ClusterVersion>
    {
        public ClusterVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        public int Major { get; }
        public int Minor { get; }

        public static ClusterVersion FromVersion(Version version)
        {
            return new ClusterVersion(version.Major, version.Minor);
        }

        public static ClusterVersion FromVersionInfo(VersionInfo versionInfo)
        {
            return new ClusterVersion(SanitizeAndParseVersionNumber(versionInfo.Major), SanitizeAndParseVersionNumber(versionInfo.Minor));
        }

        static int SanitizeAndParseVersionNumber(string version)
        {
            return int.Parse(Regex.Replace(version, "[^0-9]", ""));
        }

        public int CompareTo(ClusterVersion? other)
        {
            if (other == null) return 1;
            if (Major > other.Major || (Major == other.Major && Minor > other.Minor)) return 1;
            if (Major == other.Major && Minor == other.Minor) return 0;
            return -1;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not ClusterVersion clusterVersion)
                return false;

            return clusterVersion.Major == Major && clusterVersion.Minor == Minor;
        }

        public override int GetHashCode()
        {
#if NET8_0_OR_GREATER
            return HashCode.Combine(Major, Minor);
#else
            unchecked // Overflow is fine in hash code calculations
            {
                int hash = 17;
                hash = hash * 23 + Major.GetHashCode();
                hash = hash * 23 + Minor.GetHashCode();
                return hash;
            }
#endif
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}";
        }
    }
}