using System;
using System.Collections.Concurrent;
using System.Threading;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class ScriptIsolationMutex
    {
        // Reader-writer locks allow multiple readers, but only one writer which blocks readers. This is perfect for our scenario, because 
        // we want to allow lots of scripts to run with the 'no' isolation level, but nothing should be running under the 'full' isolation level.
        static readonly ConcurrentDictionary<string, ReaderWriterLockSlim> ReaderWriterLocks = new ConcurrentDictionary<string, ReaderWriterLockSlim>();

        public static IDisposable Acquire(ScriptIsolationLevel isolation, string lockName, Action<string> log)
        {
            var readerWriter = GetLock(lockName);
            switch (isolation)
            {
                case ScriptIsolationLevel.FullIsolation:
                    return EnterWriteLock(log, readerWriter);
                case ScriptIsolationLevel.NoIsolation:
                    return EnterReadLock(log, readerWriter);
            }

            throw new NotSupportedException("Unknown isolation level: " + isolation);
        }

        private static IDisposable EnterReadLock(Action<string> log, ReaderWriterLockSlim readerWriter)
        {
            if (!readerWriter.TryEnterReadLock(100))
            {
                Busy(log);
                readerWriter.EnterReadLock();
            }
            return new CallbackDisposable(readerWriter.ExitReadLock);
        }

        private static IDisposable EnterWriteLock(Action<string> log, ReaderWriterLockSlim readerWriter)
        {
            if (!readerWriter.TryEnterWriteLock(100))
            {
                Busy(log);
                readerWriter.EnterWriteLock();
            }
            return new CallbackDisposable(readerWriter.ExitWriteLock);
        }

        static ReaderWriterLockSlim GetLock(string lockName)
        {
            return ReaderWriterLocks.GetOrAdd(lockName, new ReaderWriterLockSlim());
        }
        

        static void Busy(Action<string> log)
        {
            log("This Tentacle is currently busy performing a task that cannot be run in conjunction with any other task. Please wait...");
        }
    }
}