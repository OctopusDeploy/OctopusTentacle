using System;
using System.IO;
using NuGet;
using Octopus.Platform.Util;

namespace Octopus.Shared.Packages
{
    public class FixedFilePathResolver : IPackagePathResolver
    {
        readonly string packageName;
        readonly string filePathNameToReturn;

        public FixedFilePathResolver(string packageName, string filePathNameToReturn)
        {
            Guard.ArgumentNotNull(packageName, "packageName");
            Guard.ArgumentNotNull(filePathNameToReturn, "filePathNameToReturn");
            this.packageName = packageName;
            this.filePathNameToReturn = filePathNameToReturn;
        }

        public string GetInstallPath(IPackage package)
        {
            EnsureRightPackage(package.Id);
            return Path.GetDirectoryName(filePathNameToReturn);
        }

        public string GetPackageDirectory(IPackage package)
        {
            return GetPackageDirectory(package.Id, package.Version);
        }

        public string GetPackageFileName(IPackage package)
        {
            return GetPackageFileName(package.Id, package.Version);
        }

        public string GetPackageDirectory(string packageId, SemanticVersion version)
        {
            EnsureRightPackage(packageId);
            return string.Empty;
        }

        public string GetPackageFileName(string packageId, SemanticVersion version)
        {
            EnsureRightPackage(packageId);
            return Path.GetFileName(filePathNameToReturn);
        }

        void EnsureRightPackage(string packageId)
        {
            var samePackage = string.Equals(packageId, packageName, StringComparison.InvariantCultureIgnoreCase);

            if (!samePackage)
            {
                throw new ArgumentException(string.Format("Expected to be asked for the path for package {0}, but was instead asked for the path for package {1}", packageName, packageId));
            }
        }
    }
}