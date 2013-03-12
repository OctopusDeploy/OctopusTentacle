using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using NuGet;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    /// <summary>
    /// Given a 180mb NuGet package, NuGet.Core's PackageManager uses 1.17GB of memory and 55 seconds to extract it. 
    /// This is because it continually takes package files and copies them to byte arrays in memory to work with. 
    /// This class simply uses the packaging API's directly to extract, and only uses 6mb and takes 10 seconds on the 
    /// same 180mb file. 
    /// </summary>
    public class LightweightPackageExtractor : IPackageExtractor
    {
        readonly IOctopusFileSystem fileSystem;
        static readonly string[] ExcludePaths = new[] { "_rels", "package\\services\\metadata" };

        public LightweightPackageExtractor(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(string packageFile, string directory)
        {
            using (var package = Package.Open(packageFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var files =
                    from part in package.GetParts()
                    where IsPackageFile(part)
                    select part;

                foreach (var part in files)
                {
                    var path = UriUtility.GetPath(part.Uri);
                    path = Path.Combine(directory, path);

                    var parent = Path.GetDirectoryName(path);
                    fileSystem.EnsureDirectoryExists(parent);

                    using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var stream = part.GetStream())
                    {
                        stream.CopyTo(fileStream);
                        fileStream.Flush();
                    }
                }
            }
        }

        #region Code taken from nuget.codeplex.com, license: http://nuget.codeplex.com/license

        internal static bool IsPackageFile(PackagePart part)
        {
            var path = UriUtility.GetPath(part.Uri);
            return !ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                   !PackageUtility.IsManifest(path);
        }

        static class UriUtility
        {
            internal static string GetPath(Uri uri)
            {
                var path = uri.OriginalString;
                if (path.StartsWith("/", StringComparison.Ordinal))
                {
                    path = path.Substring(1);
                }
                return Uri.UnescapeDataString(path.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        static class PackageUtility
        {
            public static bool IsManifest(string path)
            {
                var extension = Path.GetExtension(path);
                return extension != null && extension.Equals(Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion
    }
}