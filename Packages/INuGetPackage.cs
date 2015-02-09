using System;
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
    }
}