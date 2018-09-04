using System;
using System.Threading;

namespace Octopus.Shared.Threading
{
    public interface IMachineWideMutex
    {
        IDisposable Acquire(string name, CancellationToken cancellationToken);
        IDisposable Acquire(string name, string waitMessage, CancellationToken cancellationToken);
    }
}