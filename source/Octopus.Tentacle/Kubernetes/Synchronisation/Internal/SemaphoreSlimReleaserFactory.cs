using System;
using System.Threading;

namespace Octopus.Tentacle.Kubernetes.Synchronisation.Internal
{
    class SemaphoreSlimReleaserFactory<T> : ISemaphoreSlimReleaserFactory<T> where T : SemaphoreSlim
    {
        public ISemaphoreSlimReleaser<T> Create(T semaphoreSlim, Action onDispose)
        {
            return new SemaphoreSlimReleaser<T>(semaphoreSlim, onDispose);
        }
    }
}