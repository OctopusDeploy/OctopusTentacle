using System;
using NuGet;
using Octopus.Shared.Packages;

namespace Octopus.Shared.BuiltInFeed
{
    public interface IBuiltInPackageRepositoryFactory
    {
        bool IsBuiltInSource(string packageSource);
        INuGetRepository CreateRepository();
        BuiltInRepositoryStatistics GetStatus();
        void RemovePackage(INuGetPackage package);
        void AddPackage(IPackage package);
    }

    public class BuiltInRepositoryStatistics
    {
        public int TotalPackages { get; set; }
        public SynchronizationState SynchronizationState { get; set; }
        public SynchronizationState IndexingState { get; set; }
        public int CompletedPackages { get; set; }
        public int PackagesToIndex { get; set; }
    }

    public enum SynchronizationState
    {
        Idle,
        Indexing
    }
}
