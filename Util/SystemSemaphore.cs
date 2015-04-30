using System;
using System.IO;
using System.Threading;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public class SystemSemaphore : ISemaphore
    {
        readonly ILog log = Log.Octopus();

        public IDisposable Acquire(string name)
        {
            return Acquire(name, null);
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            var semaphore = new Semaphore(1, 1, Normalize(name));
            if (!semaphore.WaitOne(3000))
            {
                if (waitMessage != null)
                    log.Verbose(waitMessage);
                semaphore.WaitOne();
            }

            return new SemaphoreReleaser(semaphore);
        }

        static string Normalize(string name)
        {
            return name.Replace("\\", "_").Replace(":", "_").ToLowerInvariant();
        }

        class SemaphoreReleaser : IDisposable
        {
            readonly Semaphore semaphore;

            public SemaphoreReleaser(Semaphore semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                semaphore.Release();
                semaphore.Dispose();
            }
        }
    }
}
