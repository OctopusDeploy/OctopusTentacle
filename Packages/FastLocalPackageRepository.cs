using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using Octopus.Platform.Diagnostics;

namespace Octopus.Shared.Packages
{
    /// <summary>
    /// The local package repository (used for reading packages from the file system) is broken in NuGet.Core because 
    /// when a package is being written, it refuses to read the packages. This implementation wraps it by ignoring packages
    /// that can't be written (either because they are locked or only partially written). 
    /// </summary>
    public class FastLocalPackageRepository : LocalPackageRepository
    {
        readonly ILog log;

        public FastLocalPackageRepository(string physicalPath, ILog log)
            : base(physicalPath)
        {
            this.log = log;
        }

        protected override IPackage OpenPackage(string path)
        {
            var fullPath = FileSystem.GetFullPath(path);

            try
            {
                return new ZipPackage(fullPath);
            }
            catch (Exception ex)
            {
                log.Warn(ex, "Unable to read NuGet package file: " + fullPath + " -- it will be ignored. Error: " + ex.Message);
                return new NullPackage();
            }
        }

        public override IQueryable<IPackage> GetPackages()
        {
            return base.GetPackages().Where(p => p is NullPackage == false);
        }

        /// <summary>
        /// Represents a package file that cannot be read.
        /// </summary>
        public class NullPackage : IPackage
        {
            public NullPackage()
            {
                Id = "null";
                Version = new SemanticVersion("0.0.0.0");
            }

            public string Id { get; private set; }
            public SemanticVersion Version { get; private set; }
            public string Title { get; private set; }
            public IEnumerable<string> Authors { get; private set; }
            public IEnumerable<string> Owners { get; private set; }
            public Uri IconUrl { get; private set; }
            public Uri LicenseUrl { get; private set; }
            public Uri ProjectUrl { get; private set; }
            public bool RequireLicenseAcceptance { get; private set; }
            public string Description { get; private set; }
            public string Summary { get; private set; }
            public string ReleaseNotes { get; private set; }
            public string Language { get; private set; }
            public string Tags { get; private set; }
            public string Copyright { get; private set; }
            public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; private set; }
            public ICollection<PackageReferenceSet> PackageAssemblyReferences { get; private set; }
            public IEnumerable<PackageDependencySet> DependencySets { get; private set; }
            public Version MinClientVersion { get; private set; }
            public Uri ReportAbuseUrl { get; private set; }
            public int DownloadCount { get; private set; }
            public IEnumerable<IPackageFile> GetFiles()
            {
                throw new NotImplementedException();
            }

            public IEnumerable<FrameworkName> GetSupportedFrameworks()
            {
                throw new NotImplementedException();
            }

            public Stream GetStream()
            {
                throw new NotImplementedException();
            }

            public bool IsAbsoluteLatestVersion { get; private set; }
            public bool IsLatestVersion { get; private set; }
            public bool Listed { get; private set; }
            public DateTimeOffset? Published { get; private set; }
            public IEnumerable<IPackageAssemblyReference> AssemblyReferences { get; private set; }
        }
    }
}