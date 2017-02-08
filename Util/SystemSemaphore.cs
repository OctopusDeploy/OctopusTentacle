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
        static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();
        static readonly string VolumeSeparatorString = Path.VolumeSeparatorChar.ToString();


        public IDisposable Acquire(string name)
        {
            return Acquire(name, null);
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            systemLog.Trace($"Aquiring system semaphore {name}");
            var semaphore = new Semaphore(1, 1, Normalize(name));
            if (!semaphore.WaitOne(3000))
            {
                systemLog.Verbose($"System semaphore {name} in use, waiting. {waitMessage}");
                if (waitMessage != null)
                    log.Verbose(waitMessage);
                semaphore.WaitOne();
            }

            systemLog.Trace($"Aquired system semaphore {name}");
            return new SemaphoreReleaser(semaphore, name);
        }

        static string Normalize(string name)
        {
            return name.Replace(DirectorySeparatorString, "_").Replace(VolumeSeparatorString, "_").ToLowerInvariant();
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
                Log.System().Trace($"Released system semaphore {name}");
            }
        }
    }
}