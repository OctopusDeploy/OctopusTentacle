using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Octopus.Time;

namespace Octopus.Shared.Threading
{
    /// <summary>
    /// Problem:
    /// Using a named semaphore as we previously did does not currently exist in linux and usage
    /// <code>new Semaphore(1, 2, "Foobar");</code>
    /// results in:
    /// <exception cref="System.PlatformNotSupportedException">The named version of this synchronization primitive is not supported on this platform</exception>
    ///
    /// As per previous usage, what this class should achieve is non-thread affinity (like a semaphore) while at the same time dealing with situations where
    /// the owning process might be killed however it does not need to be cross process. This lock can only be acquired ONCE and once acquired it MUST be released
    /// or the finalizer will attempt to release it to avoid abandoned locks.
    ///
    /// </summary>
    public class CrossPlatformNamedSemaphore : IDisposable
    {

        //Named semaphores aren't available in linux so lets fake it, by using a dictionary of semaphores.
        static readonly Dictionary<string, SemaphoreRecord> Semaphores = new Dictionary<string, SemaphoreRecord>();
        static readonly object LockObj = new object();

        readonly string name;
        readonly ISleep sleeper;
        bool isLockOwner = false;
        bool disposed = false;

        public CrossPlatformNamedSemaphore(string name, ISleep sleeper)
        {
            this.name = name;
            this.sleeper = sleeper;
            AddSemaphoreRecord();
        }

        public bool TryAcquire(TimeSpan timeout, CancellationToken token)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var totalMillisecondsToWait = timeout.TotalMilliseconds;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (TryAcquireSemaphore())
                    return true;
                if (stopwatch.ElapsedMilliseconds >= totalMillisecondsToWait)
                    return false;

                sleeper.For(100);
            }
        }

        bool TryAcquireSemaphore()
        {
            lock (LockObj)
            {
                if (isLockOwner)
                {
                    throw new SynchronizationLockException($"{nameof(CrossPlatformNamedSemaphore)} can only be acquired once.");
                }

                isLockOwner = (Semaphores[name].Semaphore.WaitOne(TimeSpan.Zero));
                return isLockOwner;
            }
        }

        void AddSemaphoreRecord()
        {
            lock (LockObj)
            {
                if (!Semaphores.TryGetValue(name, out SemaphoreRecord semaphore))
                {
                    semaphore = new SemaphoreRecord();
                    Semaphores.Add(name, semaphore);
                }
                semaphore.UsageCount++;
            }
        }

        public void ReleaseLock()
        {
            lock (LockObj)
            {
                if (!isLockOwner)
                {
                    throw new SynchronizationLockException($"Only the owner of an {nameof(CrossPlatformNamedSemaphore)} lock can release it.");
                }

                if (Semaphores.TryGetValue(name, out var record))
                {
                    record.Semaphore.Release();
                }
                isLockOwner = false;
            }
        }

        void CleanupSemaphoreRecord()
        {
            if (disposed)
            {
                return;
            }

            lock (LockObj)
            {
                if (!Semaphores.TryGetValue(name, out var record))
                    return;

                if (isLockOwner)
                {
                    record.Semaphore.Release();
                }

                if (--record.UsageCount == 0)
                {
                    Semaphores.Remove(name);
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            CleanupSemaphoreRecord();
            GC.SuppressFinalize(this);
        }

        ~CrossPlatformNamedSemaphore()
        {
            CleanupSemaphoreRecord();
        }

        class SemaphoreRecord
        {
            public Semaphore Semaphore { get; } = new Semaphore(1, 1);
            public int UsageCount { get; set; } = 0;
        }
    }
}
