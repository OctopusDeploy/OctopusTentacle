using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Scripts
{
    public class ScriptIsolationMutex
    {
        // Reader-writer locks allow multiple readers, but only one writer which blocks readers. This is perfect for our scenario, because 
        // we want to allow lots of scripts to run with the 'no' isolation level, but nothing should be running under the 'full' isolation level.
        // NOTE: Changed from ReaderWriterLockSlim to AsyncReaderWriterLock to enable cooperative cancellation whilst waiting for the lock.
        //       Hopefully in a future version of .NET there will be a fully supported ReaderWriterLock with cooperative cancellation support so we can remove this dependency.
        static readonly ConcurrentDictionary<string, AsyncReaderWriterLock> ReaderWriterLocks = new ConcurrentDictionary<string, AsyncReaderWriterLock>();
        static readonly TimeSpan InitialWaitTime = TimeSpan.FromMilliseconds(100);

        public static readonly TimeSpan NoTimeout = TimeSpan.Zero;

        public static IDisposable Acquire(ScriptIsolationLevel isolation, TimeSpan mutexAcquireTimeout, string lockName, Action<string> log, CancellationToken ct = default(CancellationToken))
        {
            var readerWriter = GetLock(lockName);
            switch (isolation)
            {
                case ScriptIsolationLevel.FullIsolation:
                    return EnterWriteLock(log, readerWriter, ct, mutexAcquireTimeout);
                case ScriptIsolationLevel.NoIsolation:
                    return EnterReadLock(log, readerWriter, ct, mutexAcquireTimeout);
            }

            throw new NotSupportedException("Unknown isolation level: " + isolation);
        }

        static IDisposable EnterReadLock(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct, TimeSpan mutexAcquireTimeout)
        {
            IDisposable lockReleaser;
            if (readerWriter.TryEnterReadLock(InitialWaitTime, ct, out lockReleaser))
            {
                return lockReleaser;
            }

            Busy(log);

            return mutexAcquireTimeout == NoTimeout
                ? EnterReadLock(log, readerWriter, ct)
                : EnterReadLockWithTimeout(log, readerWriter, ct, mutexAcquireTimeout);
        }

        static IDisposable EnterReadLockWithTimeout(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct, TimeSpan mutexAcquireTimeout)
        {
            IDisposable lockReleaser;
            if (readerWriter.TryEnterReadLock(mutexAcquireTimeout, ct, out lockReleaser))
            {
                return lockReleaser;
            }

            if (ct.IsCancellationRequested)
            {
                Canceled(log);
                throw new OperationCanceledException(ct);
            }

            TimedOut(log, mutexAcquireTimeout);
            throw new TimeoutException($"Could not acquire read mutex within timeout {mutexAcquireTimeout}.");
        }

        static IDisposable EnterReadLock(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct)
        {
            try
            {
                return readerWriter.ReaderLock(ct);
            }
            catch (TaskCanceledException tce)
            {
                Canceled(log);
                throw new OperationCanceledException(tce.CancellationToken);
            }
        }

        static IDisposable EnterWriteLock(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct, TimeSpan mutexAcquireTimeout)
        {
            IDisposable lockReleaser;
            if (readerWriter.TryEnterWriterLock(InitialWaitTime, ct, out lockReleaser))
            {
                return lockReleaser;
            }

            Busy(log);

            return mutexAcquireTimeout == TimeSpan.Zero
                ? EnterWriteLock(log, readerWriter, ct)
                : EnterWriteLockWithTimeout(log, readerWriter, ct, mutexAcquireTimeout);
        }

        static IDisposable EnterWriteLockWithTimeout(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct, TimeSpan mutexAcquireTimeout)
        {
            IDisposable lockReleaser;
            if (readerWriter.TryEnterWriterLock(mutexAcquireTimeout, ct, out lockReleaser))
            {
                return lockReleaser;
            }

            if (ct.IsCancellationRequested)
            {
                Canceled(log);
                throw new OperationCanceledException(ct);
            }

            TimedOut(log, mutexAcquireTimeout);
            throw new TimeoutException($"Could not acquire write mutex within timeout {mutexAcquireTimeout}.");
        }

        static IDisposable EnterWriteLock(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct)
        {
            try
            {
                return readerWriter.WriterLock(ct);
            }
            catch (TaskCanceledException tce)
            {
                Canceled(log);
                throw new OperationCanceledException(tce.CancellationToken);
            }
        }

        static AsyncReaderWriterLock GetLock(string lockName)
        {
            return ReaderWriterLocks.GetOrAdd(lockName, new AsyncReaderWriterLock());
        }

        static void Busy(Action<string> log)
        {
            log("Cannot start this task yet. There is already another task running that cannot be run in conjunction with any other task. Please wait...");
        }

        static void Canceled(Action<string> log)
        {
            log("This task was canceled before it could start. The other task is still running.");
        }

        static void TimedOut(Action<string> log, TimeSpan timeout)
        {
            log($"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. The other task is still running.");
        }
    }
}