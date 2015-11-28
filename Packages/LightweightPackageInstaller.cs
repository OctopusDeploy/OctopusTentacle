using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    /// <summary>
    /// Given a 180mb NuGet package, NuGet.Core's PackageManager uses 1.17GB of memory and 55 seconds to extract it.
    /// This is because it continually takes package files and copies them to byte arrays in memory to work with.
    /// This class simply uses the packaging API's directly to extract, and only uses 6mb and takes 10 seconds on the
    /// same 180mb file.
    /// </summary>
    public class LightweightPackageInstaller : IPackageInstaller
    {
        static readonly string[] ExcludePaths = {"_rels", Path.Combine("package", "services", "metadata")};
        readonly IOctopusFileSystem fileSystem;

        public LightweightPackageInstaller(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }


        public int Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning)
        {
            using (var package = Package.Open(packageFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Install(package, directory, log, suppressNestedScriptWarning);
            }
        }

        public int Install(Stream packageStream, string directory, ILog log, bool suppressNestedScriptWarning)
        {
            using (var package = Package.Open(packageStream, FileMode.Open, FileAccess.Read))
            {
                return Install(package, directory, log, suppressNestedScriptWarning);
            }
        }

        int Install(Package package, string directory, ILog log, bool suppressNestedScriptWarning)
        {
           var  filesExtracted = 0;

            var files =
                from part in package.GetParts()
                where IsPackageFile(part)
                select part;

            foreach (var part in files)
            {
                filesExtracted++;
                var path = UriUtility.GetPath(part.Uri);

                if (!suppressNestedScriptWarning)
                {
                    WarnIfScriptInSubFolder(path, log);
                }

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
            return filesExtracted;
        }

        void WarnIfScriptInSubFolder(string path, ILog log)
        {
            var fileName = Path.GetFileName(path);
            var directoryName = Path.GetDirectoryName(path);

            if (string.Equals(fileName, "Deploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PreDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PostDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "DeployFailed.ps1", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(directoryName))
                {
                    log.WarnFormat("The script file \"{0}\" contained within the package will not be executed because it is contained within a child folder. As of Octopus Deploy 2.4, scripts in sub folders will not be executed.", path);
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
                return extension != null && extension.Equals(".nuspec", StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion
    }
}