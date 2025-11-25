using System;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Packages
{
    public class NuGetPackageInstaller : IPackageInstaller
    {
        readonly IOctopusFileSystem fileSystem;

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
            // The 'source' parameter is used for logging/tracking purposes in NuGet's new API.
            // It can be null or any string identifier. We use null here as we don't need tracking.
            // Signature verification is bypassed by passing null for ClientPolicyContext (3rd param in PackageExtractionContext).
            var extracted = PackageExtractor.ExtractPackageAsync(
                    null, // source - not needed for our use case and can be null because ClientPolicyContext is null.
                    packageStream,
                    new SuppliedDirectoryPackagePathResolver(directory),
                    new PackageExtractionContext(
                        PackageSaveMode.Files,
                        XmlDocFileSaveMode.None,
                        null, // ClientPolicyContext - null bypasses signature verification
                        NullLogger.Instance)
                    {
                        CopySatelliteFiles = false
                    },
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

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