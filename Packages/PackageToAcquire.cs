using System;
using System.IO;
using System.Threading;

namespace Octopus.Shared.Packages
{
    public class PackageToAcquire
    {
        readonly Lazy<byte[]> packageStream;

        public PackageToAcquire(IPackageAcquirer packageAcquirer, PackageMetadata package, IFeed feed, PackageCachePolicy packageCachePolicy)
        {
            packageStream = new Lazy<byte[]>(() =>
            {
                var downloadedPackage = packageAcquirer.Download(package, feed, packageCachePolicy);
                var length = downloadedPackage.Length;
                var buffer = new byte[1024*128];
                while (length > 0)
                {
                    var read = downloadedPackage.Read(buffer, 0, (int) Math.Min(buffer.Length, length));
                    length -= read;
                }
                return buffer;
            });
        }

        public byte[] Download()
        {
            return packageStream.Value;
        }
    }
}
