using System;
using System.Collections.Generic;
using Octopus.Client.Model;

namespace Octopus.Shared.Packages
{
    public interface INuGetPackage
    {
        string PackageId { get; }
        SemanticVersion Version { get; }
        string Description { get; }
        string ReleaseNotes { get; }
        DateTimeOffset? Published { get; }
        string Title { get; }
        string Summary { get; }
        bool IsReleaseVersion();
        long GetSize();
        List<string> GetDependencies();
        string CalculateHash();
    }
}