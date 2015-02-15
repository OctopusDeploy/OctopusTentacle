using System;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public interface ITaskContext
    {
        string TaskId { get; }
        bool IsCancellationRequested { get; }
        CancellationToken CancellationToken { get; }
        void EnsureNotCancelled();
        void Pause();
        bool IsPaused();
    }
}