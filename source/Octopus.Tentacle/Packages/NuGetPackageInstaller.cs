using System;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Packages
{
    public class NuGetPackageInstaller : IPackageInstaller
    {
        private readonly IOctopusFileSystem fileSystem;

        public NuGetPackageInstaller(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public int Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning)
        {
            using (var packageStream = fileSystem.OpenFile(packageFile, FileMode.Open, FileAccess.Read))
            {
                return Install(packageStream, directory, log, suppressNestedScriptWarning);
            }
        }

        public int Install(Stream packageStream, string directory, ILog log, bool suppressNestedScriptWarning)
        {
            var extracted = PackageExtractor.ExtractPackage(packageStream,
                new SuppliedDirectoryPackagePathResolver(directory),
                new PackageExtractionContext(NullLogger.Instance) { PackageSaveMode = PackageSaveMode.Files, XmlDocFileSaveMode = XmlDocFileSaveMode.None, CopySatelliteFiles = false },
                CancellationToken.None);

            return extracted.Count();
        }

        private class SuppliedDirectoryPackagePathResolver : PackagePathResolver
        {
            public SuppliedDirectoryPackagePathResolver(string packageDirectory) : base(packageDirectory, false)
            {
            }

            public override string GetInstallPath(PackageIdentity packageIdentity)
            {
                return Root;
            }
        }
    }
}