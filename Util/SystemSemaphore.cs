using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public class SystemSemaphore : ISemaphore
    {
        readonly ILog log = Log.Octopus();

        public IDisposable Acquire(string name, string waitMessage)
        {
            var semaphore = new Semaphore(1, 1, name);
            if (!semaphore.WaitOne(1000))
            {
                log.Verbose(waitMessage);
                semaphore.WaitOne();
            }

            return new SemaphoreReleaser(semaphore);
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
