using System;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public interface ITaskContext
    {
        string Id { get; }
        bool IsCancellationRequested { get; }
        CancellationToken CancellationToken { get; }
        void EnsureNotCancelled();
        void Pause();
        bool IsPaused();
    }
}