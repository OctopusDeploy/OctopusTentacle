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
        static readonly ConcurrentDictionary<string, AsyncReaderWriterLock> ReaderWriterLocks = new ConcurrentDictionary<string, AsyncReaderWriterLock>();
        static readonly TimeSpan InitialWaitTime = TimeSpan.FromMilliseconds(100);

        public static IDisposable Acquire(ScriptIsolationLevel isolation, string lockName, Action<string> log, CancellationToken ct = default(CancellationToken))
        {
            var readerWriter = GetLock(lockName);
            switch (isolation)
            {
                case ScriptIsolationLevel.FullIsolation:
                    return EnterWriteLock(log, readerWriter, ct);
                case ScriptIsolationLevel.NoIsolation:
                    return EnterReadLock(log, readerWriter, ct);
            }

            throw new NotSupportedException("Unknown isolation level: " + isolation);
        }

        static IDisposable EnterReadLock(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct)
        {
            IDisposable lockReleaser;
            if (readerWriter.TryEnterReadLock(InitialWaitTime, out lockReleaser))
            {
                return lockReleaser;
            }

            Busy(log);

            try
            {
                return readerWriter.ReaderLock(ct);
            }
            catch (TaskCanceledException tce)
            {
                Cancelled(log);
                throw new OperationCanceledException(tce.CancellationToken);
            }
        }

        static IDisposable EnterWriteLock(Action<string> log, AsyncReaderWriterLock readerWriter, CancellationToken ct)
        {
            IDisposable lockReleaser;
            if (readerWriter.TryEnterWriterLock(InitialWaitTime, out lockReleaser))
            {
                return lockReleaser;
            }

            Busy(log);

            try
            {
                return readerWriter.WriterLock(ct);
            }
            catch (TaskCanceledException tce)
            {
                Cancelled(log);
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

        static void Cancelled(Action<string> log)
        {
            log("This task was cancelled before it could start. The other task had not completed.");
        }
    }

    public static class AsyncReaderWriterLockExtensions
    {
        public static bool TryEnterReadLock(this AsyncReaderWriterLock @lock, TimeSpan timeout, out IDisposable releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            {
                try
                {
                    releaseLock = @lock.ReaderLock(timeoutSource.Token);
                    return true;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
        }

        public static bool TryEnterWriterLock(this AsyncReaderWriterLock @lock, TimeSpan timeout, out IDisposable releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            {
                try
                {
                    releaseLock = @lock.WriterLock(timeoutSource.Token);
                    return true;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
        }
    }
}