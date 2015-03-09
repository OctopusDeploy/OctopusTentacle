using System;
using System.IO;
using System.Threading;

namespace Octopus.Shared.Packages
{
    public class PackageToAcquire
    {
        readonly Lazy<Stream> packageStream;

        public PackageToAcquire(IPackageAcquirer packageAcquirer, PackageMetadata package, IFeed feed, PackageCachePolicy packageCachePolicy)
        {
            packageStream = new Lazy<Stream>(() => 
                packageAcquirer.Download(package, feed, packageCachePolicy));
        }

        public Stream Download()
        {
            return packageStream.Value;
        }
    }
}
