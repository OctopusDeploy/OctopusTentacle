using System;

namespace Octopus.Shared.BuiltInFeed
{
    public class RepositoryStatistics
    {
        public int TotalPackages { get; set; }
        public SynchronizationState SynchronizationState { get; set; }
        public SynchronizationState IndexingState { get; set; }
        public int CompletedPackages { get; set; }
        public int PackagesToIndex { get; set; }
    }
}