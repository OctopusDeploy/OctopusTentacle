using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;
using Octopus.Shared.Util;

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

        public string Title
        {
            get { return wrapped.Title; }
        }

        public string Summary
        {
            get { return wrapped.Summary; }
        }

        public long GetSize()
        {
            return wrapped.GetStream().Length;
        }

        public List<string> GetDependencies()
        {
            return wrapped.DependencySets.SelectMany(ds => ds.Dependencies).Select(dependency => dependency.ToString()).ToList();
        }

        public string CalculateHash()
        {
            return HashCalculator.Hash(wrapped.GetStream());            
        }

        public IPackage GetRealPackage()
        {
            return wrapped;
        }
    }
}