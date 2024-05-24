using System;
using System.Threading;

namespace Octopus.Tentacle.Kubernetes.Synchronisation.Internal
{
    interface ISemaphoreSlimReleaser<out T> : IDisposable where T : SemaphoreSlim
    {
        public T Semaphore { get; }
    }
}