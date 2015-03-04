using System;
using NuGet;
using Octopus.Shared.Packages;

namespace Octopus.Shared.BuiltInFeed
{
    public interface IBuiltInPackageRepository
    {
        bool IsBuiltInSource(string packageSource);
        string MapPath(object packageId, string version);
        INuGetFeed CreateRepository();
        RepositoryStatistics GetStatistics();
        void RemovePackage(INuGetPackage package);
        void AddPackage(IPackage package);
    }
}
