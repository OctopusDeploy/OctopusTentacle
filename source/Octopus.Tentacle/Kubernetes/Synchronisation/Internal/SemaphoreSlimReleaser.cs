using System;
using System.Threading;

namespace Octopus.Tentacle.Kubernetes.Synchronisation.Internal
{
    class SemaphoreSlimReleaser<T> : IDisposable where T : SemaphoreSlim
    {
        readonly Action onDispose;

        public SemaphoreSlimReleaser(T semaphoreSlim, Action onDispose)
        {
            Semaphore = semaphoreSlim;
            this.onDispose = onDispose;
        }

        public T Semaphore { get; }

        public void Dispose()
        {
            Semaphore.Release();
            onDispose.Invoke();
        }
    }
}