using System;
using System.Threading;

namespace Octopus.Tentacle.Util
{
    internal class SemaphoreSlimReleaser : IDisposable
    {
        private readonly SemaphoreSlim semaphore;

        public SemaphoreSlimReleaser(SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore;
        }

        public void Dispose()
        {
            semaphore.Release();
        }
    }
}
