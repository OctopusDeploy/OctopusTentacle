using System;
using System.Collections.Generic;
using System.IO;

namespace Octopus.Shared.Packages
{
    public interface INuGetFeed
    {
        INuGetPackage GetPackage(string packageId, Client.Model.SemanticVersion version);
        List<INuGetPackage> GetVersions(string packageId, out int total, int skip = 0, int take = 30, bool allowPreRelease = true);
        List<INuGetPackage> GetPackagesContaining(string searchTerm, out int total, int skip = 0, int take = 30, bool allowPreRelease = true);
        Stream GetPackageRaw(string packageId, Client.Model.SemanticVersion version);
    }
}