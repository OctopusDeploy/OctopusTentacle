using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public class Throttle : IThrottle
    {
        static readonly TimeSpan DefaultInitialAttemptTimeout = TimeSpan.FromSeconds(3);
        static readonly TimeSpan DefaultWaitBetweenAttempts = TimeSpan.FromSeconds(60);

        readonly ILog log = Log.Octopus();
        readonly ILog systemLog = Log.System();

        readonly string name;
        readonly int size;
        readonly SemaphoreSlim semaphore;

        public Throttle(
            string name,
            int size,
            TimeSpan? initialAcquisitionAttemptTimeout = null,
            TimeSpan? waitBetweenAcquisitionAttempts = null)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), size, "It doesn't make much sense to have a throttle with no capacity.");
            InitialAttemptTimeout = initialAcquisitionAttemptTimeout ?? DefaultInitialAttemptTimeout;
            WaitBetweenAttempts = waitBetweenAcquisitionAttempts ?? DefaultWaitBetweenAttempts;
            this.name = name;
            this.size = size;
            semaphore = new SemaphoreSlim(size, size);
        }

        public TimeSpan InitialAttemptTimeout { get; }
        public TimeSpan WaitBetweenAttempts { get; }

        public IDisposable Wait(CancellationToken cancellationToken)
        {
            systemLog.Trace($"Trying to enter throttle [{ToString()}]");
            cancellationToken.ThrowIfCancellationRequested();

            // Try to acquire the semaphore for a few seconds before reporting we are going to start waiting
            if (semaphore.Wait(InitialAttemptTimeout, cancellationToken))
            {
                systemLog.Trace($"Entered throttle [{ToString()}]");
                return new ThrottleReleaser(semaphore, name, size);
            }

            // Go into an acquisition loop supporting cooperative cancellation
            while (!semaphore.Wait(WaitBetweenAttempts, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                systemLog.Verbose($"The throttle is full [{ToString()}]. Waiting for our turn...");
                log.Verbose($"The Octopus Server is busy running other tasks like this one [{ToString()}]. Waiting for our turn...");
            }

            systemLog.Trace($"Entered throttle [{ToString()}]");

            return new ThrottleReleaser(semaphore, name, size);
        }

        public override string ToString()
            => $"{name}:{size - semaphore.CurrentCount}/{size}";

        class ThrottleReleaser : IDisposable
        {
            readonly SemaphoreSlim semaphore;
            readonly int size;
            readonly string name;

            public ThrottleReleaser(SemaphoreSlim semaphore, string name, int size)
            {
                this.semaphore = semaphore;
                this.name = name;
                this.size = size;
            }

            public void Dispose()
            {
                try
                {
                    semaphore.Release();
                }
                catch (Exception ex)
                {
                    Log.System().Warn(ex, $"Exception thrown while releasing the throttle [{name}]");
                }

                Log.System().Trace($"Released throttle [{ToString()}]");
            }

            public override string ToString()
                => $"{name}:{semaphore.CurrentCount}/{size}";
        }
    }
}