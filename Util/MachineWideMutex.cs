using System;
using System.IO;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public class MachineWideMutex : IMachineWideMutex
    {
        static readonly TimeSpan DefaultInitialAcquisitionAttemptTimeout = TimeSpan.FromSeconds(3);
        static readonly TimeSpan DefaultWaitBetweenAcquisitionAttempts = TimeSpan.FromSeconds(60);

        readonly CancellationToken cancellationToken;
        readonly ILog log = Log.Octopus();
        readonly ILog systemLog = Log.System();
        static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();
        static readonly string VolumeSeparatorString = Path.VolumeSeparatorChar.ToString();

        public MachineWideMutex(
            CancellationToken cancellationToken = default(CancellationToken),
            TimeSpan? initialAcquisitionAttemptTimeout = null,
            TimeSpan? waitBetweenAcquisitionAttempts = null)
        {
            this.cancellationToken = cancellationToken;
            InitialAcquisitionAttemptTimeout = initialAcquisitionAttemptTimeout ?? DefaultInitialAcquisitionAttemptTimeout;
            WaitBetweenAcquisitionAttempts = waitBetweenAcquisitionAttempts ?? DefaultWaitBetweenAcquisitionAttempts;
        }

        public TimeSpan InitialAcquisitionAttemptTimeout { get; }
        public TimeSpan WaitBetweenAcquisitionAttempts { get; }

        public IDisposable Acquire(string name)
        {
            return Acquire(name, null);
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            systemLog.Trace($"Acquiring machine-wide mutex {name}");
            cancellationToken.ThrowIfCancellationRequested();

            // Create a new named Semaphore - note this is cross-process
            var semaphore = new Semaphore(1, 1, Normalize(name));

            // Try to acquire the semaphore for a few seconds before reporting we are going to start waiting
            if (AcquireSemaphore(semaphore, cancellationToken, InitialAcquisitionAttemptTimeout))
            {
                systemLog.Trace($"Acquired machine-wide mutex {name}");
                return new MachineWideMutexReleaser(semaphore, name);
            }

            // Go into an acquisition loop supporting cooperative cancellation
            while (!AcquireSemaphore(semaphore, cancellationToken, WaitBetweenAcquisitionAttempts))
            {
                cancellationToken.ThrowIfCancellationRequested();

                systemLog.Verbose($"System machine-wide mutex {name} in use, waiting. {waitMessage}");
                if (!string.IsNullOrWhiteSpace(waitMessage))
                    log.Verbose(waitMessage);
            }

            systemLog.Trace($"Acquired machine-wide mutex {name}");
            return new MachineWideMutexReleaser(semaphore, name);
        }

        static bool AcquireSemaphore(Semaphore semaphore, CancellationToken cancellationToken, TimeSpan attemptTimeout)
        {
            // Wait on either acquiring the semaphore or cancellation being signalled, for up to the time allotted for this attempt
            var waitHandles = new[] { semaphore, cancellationToken.WaitHandle };
            var waitResult = WaitHandle.WaitAny(waitHandles, attemptTimeout);

            // Beware the result may not be an index in the array hence checking WaitHandle.WaitTimeout first.
            return waitResult != WaitHandle.WaitTimeout && waitHandles[waitResult] == semaphore;
        }

        static string Normalize(string name)
        {
            return name.Replace(DirectorySeparatorString, "_").Replace(VolumeSeparatorString, "_").ToLowerInvariant();
        }

        class MachineWideMutexReleaser : IDisposable
        {
            readonly Semaphore semaphore;
            readonly string name;

            public MachineWideMutexReleaser(Semaphore semaphore, string name)
            {
                this.semaphore = semaphore;
                this.name = name;
            }

            public void Dispose()
            {
                try
                {
                    semaphore.Release();
                }
                catch (Exception ex)
                {
                    Log.System().Warn(ex, "Exception thrown while disposing machine-wide mutex");
                }

                semaphore.Dispose();

                Log.System().Trace($"Released machine-wide mutex {name}");
            }
        }
    }
}