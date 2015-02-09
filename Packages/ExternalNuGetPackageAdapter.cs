using System;
using NuGet;

namespace Octopus.Shared.Packages
{
    public class ExternalNuGetPackageAdapter : IExternalPackage
    {
        readonly IPackage wrapped;

        public ExternalNuGetPackageAdapter(IPackage wrapped)
        {
            this.wrapped = wrapped;
        }

        public string PackageId
        {
            get { return wrapped.Id; }
        }

        public Client.Model.SemanticVersion Version
        {
            get { return Client.Model.SemanticVersion.Parse(wrapped.Version.ToString()); }
        }

        public string Description
        {
            get { return wrapped.Description; }
        }

        public string ReleaseNotes
        {
            get { return wrapped.ReleaseNotes; }
        }

        public bool IsReleaseVersion()
        {
            return wrapped.IsReleaseVersion();
        }

        public DateTimeOffset? Published
        {
            get { return wrapped.Published; }
        }

        public IPackage GetRealPackage()
        {
            return wrapped;
        }
    }
}