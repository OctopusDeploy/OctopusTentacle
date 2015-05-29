using System;
using System.Threading;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class ScriptIsolationMutex
    {
        // Reader-writer locks allow multiple readers, but only one writer which blocks readers. This is perfect for our scenario, because 
        // we want to allow lots of scripts to run with the 'no' isolation level, but nothing should be running under the 'full' isolation level.
        readonly static ReaderWriterLockSlim ReaderWriter = new ReaderWriterLockSlim();

        public static IDisposable Acquire(ScriptIsolationLevel isolation, Action<string> log)
        {
            switch (isolation)
            {
                case ScriptIsolationLevel.FullIsolation:
                    if (!ReaderWriter.TryEnterWriteLock(100))
                    {
                        Busy(log);
                        ReaderWriter.EnterWriteLock();
                    }
                    return new CallbackDisposable(() => ReaderWriter.ExitWriteLock());
                case ScriptIsolationLevel.NoIsolation:
                    if (!ReaderWriter.TryEnterReadLock(100))
                    {
                        Busy(log);
                        ReaderWriter.EnterReadLock();
                    }
                    return new CallbackDisposable(() => ReaderWriter.ExitReadLock());
            }

            throw new NotSupportedException("Unknown isolation level: " + isolation);
        }

        static void Busy(Action<string> log)
        {
            log("This Tentacle is currently busy performing a task that cannot be run in conjunction with any other task. Please wait...");
        }
    }
}