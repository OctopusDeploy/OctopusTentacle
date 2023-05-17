using System;
using System.Threading;

namespace Octopus.Tentacle.Util
{
    public static class SemaphoreSlimExtensions
    {
        public static IDisposable Lock(this SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new SemaphoreSlimReleaser(semaphore);
        }
    }
}
