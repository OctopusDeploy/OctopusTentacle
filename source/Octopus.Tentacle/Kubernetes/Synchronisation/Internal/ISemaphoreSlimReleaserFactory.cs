using System;
using System.Threading;

namespace Octopus.Tentacle.Kubernetes.Synchronisation.Internal
{
    interface ISemaphoreSlimReleaserFactory<T> where T : SemaphoreSlim
    {
        public ISemaphoreSlimReleaser<T> Create(T semaphoreSlim, Action onDispose);
    }
}