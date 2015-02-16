using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet;

namespace Octopus.Shared.Packages
{
    public class ExternalNuGetFeedAdapter : INuGetFeed
    {
        readonly IPackageRepository repository;

        public ExternalNuGetFeedAdapter(IPackageRepository repository)
        {
            this.repository = repository;
        }

        public INuGetPackage GetPackage(string packageId, Client.Model.SemanticVersion version)
        {
            var result = GetSinglePackage(packageId, version);
            return result == null ? null : WrapPackage(result);
        }

        public List<INuGetPackage> GetVersions(string packageId, out int total, int skip = 0, int take = 30, bool allowPreRelease = true)
        {
            var packages = FindPackageNamed(packageId, take).Select(WrapPackage).ToList();
            total = packages.Count;
            return packages;
        }

        public List<INuGetPackage> GetPackagesContaining(string searchTerm, out int total, int skip = 0, int take = 30, bool allowPreRelease = true)
        {
            var packages = SearchForPackagesNamedLike(searchTerm, take).Select(WrapPackage).ToList();
            total = packages.Count;
            return packages;
        }

        public Stream GetPackageRaw(string packageId, Client.Model.SemanticVersion version)
        {
            var result = GetSinglePackage(packageId, version);
            return result == null ? null : result.GetStream();
        }

        IPackage GetSinglePackage(string packageId, Client.Model.SemanticVersion version)
        {
            return repository.FindPackage(packageId, new SemanticVersion(version.ToString()));
        }

        IEnumerable<IPackage> FindPackageNamed(string packageId, int take)
        {
            return repository.FindPackagesById(packageId)
                .ToList()
                .OrderByDescending(x => x.Version)
                .Where(p => p.IsListed())
                .Take(take)
                .ToList();
        }

        IEnumerable<IPackage> SearchForPackagesNamedLike(string packageId, int take)
        {
            var searchTerm = packageId.ToLower().Trim();

            var pks = repository.GetPackages()
                .Where(p => p.Id.ToLower().Contains(searchTerm) || (p.Title != null && p.Title.ToLower().Contains(searchTerm)))
                .Where(p => p.IsLatestVersion || p.IsAbsoluteLatestVersion)
                .Take(take)
                .ToList();

            return pks
                .Where(p => p.IsListed())
                .OrderBy(o => o.Id).ThenByDescending(o => o.Version)
                .ToList();
        }

        static INuGetPackage WrapPackage(IPackage package)
        {
            return new ExternalNuGetPackageAdapter(package);
        }
    }
}