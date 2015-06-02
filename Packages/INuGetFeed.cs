using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Client.Model;

namespace Octopus.Shared.Packages
{
    public interface INuGetFeed
    {
        INuGetPackage GetPackage(string packageId, SemanticVersion version);
        List<INuGetPackage> GetVersions(string packageId, out int total, int skip = 0, int take = 30, bool allowPreRelease = true);
        List<INuGetPackage> GetPackagesContaining(string searchTerm, out int total, int skip = 0, int take = 30, bool allowPreRelease = true);
        Stream GetPackageRaw(string packageId, SemanticVersion version);
    }
}