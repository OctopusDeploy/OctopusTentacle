using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Util
{
    public static class SemaphoreSlimExtensions
    {
        public static IDisposable Lock(this SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new SemaphoreSlimReleaser(semaphore);
        }
        
        public static async Task<IDisposable> LockAsync(this SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            return new SemaphoreSlimReleaser(semaphore);
        }
    }
}
