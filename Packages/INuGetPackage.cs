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
        bool IsReleaseVersion();
        DateTimeOffset? Published { get; }
        string Title { get; }
        string Summary { get; }

        long GetSize();
        List<string> GetDependencies();
        string CalculateHash();
    }
}