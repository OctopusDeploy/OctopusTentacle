using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;

namespace Octopus.Tentacle.Packages
{
    public class NuGetPackageInstaller : IPackageInstaller
    {
        readonly IOctopusFileSystem fileSystem;

        public NuGetPackageInstaller(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public async Task<int> Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning, CancellationToken cancellationToken)
        {
            using (var packageStream = fileSystem.OpenFile(packageFile, FileMode.Open, FileAccess.Read))
            {
                return await Install(packageStream, directory, log, suppressNestedScriptWarning, cancellationToken);
            }
        }

        public async Task<int> Install(Stream packageStream, string directory, ILog log, bool suppressNestedScriptWarning, CancellationToken cancellationToken)
        {
            var extracted = await PackageExtractor.ExtractPackageAsync(
                string.Empty,
                packageStream,
                new SuppliedDirectoryPackagePathResolver(directory),
                new PackageExtractionContext(PackageSaveMode.Files, XmlDocFileSaveMode.None, ClientPolicyContext.GetClientPolicy(new NullSettings(), new NullLogger()), new NullLogger()) { CopySatelliteFiles = false }, cancellationToken);

            return extracted.Count();
        }

        class SuppliedDirectoryPackagePathResolver : PackagePathResolver
        {
            public SuppliedDirectoryPackagePathResolver(string packageDirectory) : base(packageDirectory, false)
            {
            }

            public override string GetInstallPath(PackageIdentity packageIdentity)
                => Root;
        }
    }
}