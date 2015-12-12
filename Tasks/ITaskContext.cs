using System;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public interface ITaskContext
    {
        string TaskId { get; }
        bool IsCancellationRequested { get; }
        CancellationToken CancellationToken { get; }
        void EnsureNotCanceled();
        void Pause();
        bool IsPaused();
    }
}