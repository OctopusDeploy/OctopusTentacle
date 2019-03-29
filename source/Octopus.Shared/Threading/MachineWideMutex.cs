using System;
using System.IO;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Time;

namespace Octopus.Shared.Threading
{
    public class MachineWideMutex : IMachineWideMutex
    {
        static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();
        static readonly string VolumeSeparatorString = Path.VolumeSeparatorChar.ToString();
        static readonly TimeSpan DefaultInitialAcquisitionAttemptTimeout = TimeSpan.FromSeconds(3);
        static readonly TimeSpan DefaultWaitBetweenAcquisitionAttempts = TimeSpan.FromSeconds(60);

        readonly ILog log = Log.Octopus();
        readonly ILog systemLog = Log.System();
        readonly Sleep sleeper = new Sleep();

        public MachineWideMutex(
            TimeSpan? initialAcquisitionAttemptTimeout = null,
            TimeSpan? waitBetweenAcquisitionAttempts = null)
        {
            InitialAcquisitionAttemptTimeout = initialAcquisitionAttemptTimeout ?? DefaultInitialAcquisitionAttemptTimeout;
            WaitBetweenAcquisitionAttempts = waitBetweenAcquisitionAttempts ?? DefaultWaitBetweenAcquisitionAttempts;
        }

        public TimeSpan InitialAcquisitionAttemptTimeout { get; }
        public TimeSpan WaitBetweenAcquisitionAttempts { get; }

        public IDisposable Acquire(string name, CancellationToken cancellationToken)
        {
            return Acquire(name, null, cancellationToken);
        }

        public IDisposable Acquire(string name, string waitMessage, CancellationToken cancellationToken)
        {
            systemLog.Trace($"Acquiring machine-wide mutex {name}");
            cancellationToken.ThrowIfCancellationRequested();

            var waitHandle = new CrossPlatformNamedSemaphore(Normalize(name), sleeper);
            var lockReleaser = new MachineWideMutexReleaser(waitHandle, name);
            // Create a new named Semaphore - note this is cross-process
            var semaphore = new Semaphore(1, 1);

            // Try to acquire the lock for a few seconds before reporting we are going to start waiting
            if (waitHandle.TryAcquire(InitialAcquisitionAttemptTimeout, cancellationToken))
            {
                systemLog.Trace($"Acquired process-wide mutex {name}");
                return lockReleaser;
            }


            // Go into an acquisition loop supporting cooperative cancellation
            LogWaiting(name, waitMessage);

            while (!AcquireSemaphore(semaphore, cancellationToken, WaitBetweenAcquisitionAttempts))
            {
                LogWaiting(name, waitMessage);
            }

            systemLog.Trace($"Acquired machine-wide mutex {name}");
            return lockReleaser;
        }

        static bool AcquireSemaphore(Semaphore semaphore, CancellationToken cancellationToken, TimeSpan attemptTimeout)
        {
            // Wait on either acquiring the semaphore or cancellation being signalled, for up to the time allotted for this attempt
            var waitHandles = new[] { semaphore, cancellationToken.WaitHandle };
            var waitResult = WaitHandle.WaitAny(waitHandles, attemptTimeout);

            // Beware the result may not be an index in the array hence checking WaitHandle.WaitTimeout first.
            return waitResult != WaitHandle.WaitTimeout && waitHandles[waitResult] == semaphore;
        }

        void LogWaiting(string name, string waitMessage)
        {
            systemLog.Verbose($"Machine-wide mutex {name} in use, waiting. {waitMessage}");
            if (!string.IsNullOrWhiteSpace(waitMessage))
                log.Info(waitMessage);
        }

        static string Normalize(string name)
        {
            return name.Replace(DirectorySeparatorString, "_").Replace(VolumeSeparatorString, "_").ToLowerInvariant();
        }

        class MachineWideMutexReleaser : IDisposable
        {
            readonly CrossPlatformNamedSemaphore semaphore;
            readonly string name;

            public MachineWideMutexReleaser(CrossPlatformNamedSemaphore semaphore, string name)
            {
                this.semaphore = semaphore;
                this.name = name;
            }

            public void Dispose()
            {
                try
                {
                    semaphore.ReleaseLock();
                }
                catch (Exception ex)
                {
                    Log.System().Warn(ex, $"Exception thrown while disposing machine-wide mutex {name}");
                }

                semaphore.Dispose();

                Log.System().Trace($"Released machine-wide mutex {name}");
            }
        }
    }
}