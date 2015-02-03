using System;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public interface ITaskContext
    {
        bool IsCancellationRequested { get; }
        CancellationToken CancellationToken { get; }
        void EnsureNotCancelled();
    }
}