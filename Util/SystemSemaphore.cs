using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public class SystemSemaphore : ISemaphore
    {
        readonly ILog log = Log.Octopus();
        readonly ILog systemLog = Log.System();

        public IDisposable Acquire(string name)
        {
            return Acquire(name, null);
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            var semaphore = new Semaphore(1, 1, Normalize(name));
            if (!semaphore.WaitOne(3000))
            {
                systemLog.Verbose($"System semaphore {name} in use, waiting. {waitMessage}");
                if (waitMessage != null)
                    log.Verbose(waitMessage);
                semaphore.WaitOne();
            }
            return new SemaphoreReleaser(semaphore, name);
        }

        static string Normalize(string name)
        {
            return name.Replace("\\", "_").Replace(":", "_").ToLowerInvariant();
        }

        class SemaphoreReleaser : IDisposable
        {
            readonly Semaphore semaphore;
            readonly string name;

            public SemaphoreReleaser(Semaphore semaphore, string name)
            {
                this.semaphore = semaphore;
                this.name = name;
            }

            public void Dispose()
            {
                semaphore.Release();
                semaphore.Dispose();
            }
        }
    }
}