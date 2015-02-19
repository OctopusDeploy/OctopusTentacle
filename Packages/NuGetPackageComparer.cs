using System;
using System.Collections.Generic;

namespace Octopus.Shared.Packages
{
    public class NuGetPackageComparer
    {
        sealed class PackageIdEqualityComparer : IEqualityComparer<INuGetPackage>
        {
            public bool Equals(INuGetPackage x, INuGetPackage y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }
                if (ReferenceEquals(x, null))
                {
                    return false;
                }
                if (ReferenceEquals(y, null))
                {
                    return false;
                }
                if (x.GetType() != y.GetType())
                {
                    return false;
                }
                return string.Equals(x.PackageId, y.PackageId);
            }

            public int GetHashCode(INuGetPackage obj)
            {
                return (obj.PackageId != null ? obj.PackageId.GetHashCode() : 0);
            }
        }

        static readonly IEqualityComparer<INuGetPackage> PackageIdComparerInstance = new PackageIdEqualityComparer();

        public static IEqualityComparer<INuGetPackage> PackageId
        {
            get { return PackageIdComparerInstance; }
        }
    }
}