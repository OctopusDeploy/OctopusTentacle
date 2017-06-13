using System;
using System.Collections.Concurrent;
using System.Threading;
using Nito.AsyncEx;
using Octopus.Shared.Contracts;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Scripts
{
    public class ScriptIsolationMutex : IDisposable
    {
        // Reader-writer locks allow multiple readers, but only one writer which blocks readers. This is perfect for our scenario, because 
        // we want to allow lots of scripts to run with the 'no' isolation level, but nothing should be running under the 'full' isolation level.
        // NOTE: Changed from ReaderWriterLockSlim to AsyncReaderWriterLock to enable cooperative cancellation whilst waiting for the lock.
        //       Hopefully in a future version of .NET there will be a fully supported ReaderWriterLock with cooperative cancellation support so we can remove this dependency.
        static readonly ConcurrentDictionary<string, TaskLock> ReaderWriterLocks = new ConcurrentDictionary<string, TaskLock>();
        static readonly TimeSpan InitialWaitTime = TimeSpan.FromMilliseconds(100);

        public static readonly TimeSpan NoTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

        public static IDisposable Acquire(ScriptIsolationLevel isolation, TimeSpan mutexAcquireTimeout, string lockName, Action<string> taskLog, string taskId, CancellationToken token)
        {
            var taskLock = ReaderWriterLocks.GetOrAdd(lockName, new TaskLock());

            return new ScriptIsolationMutex(isolation, taskLog, taskLock, token, mutexAcquireTimeout, lockName, taskId).EnterLock();
        }

        readonly TimeSpan mutexAcquireTimeout;
        readonly string lockName;
        readonly string taskId;
        readonly ScriptIsolationLevel isolationLevel;
        readonly Action<string> taskLog;
        readonly TaskLock taskLock;
        readonly CancellationToken cancellationToken;
        readonly ILogWithContext systemLog;
        readonly string lockType;
        IDisposable lockReleaser;

        ScriptIsolationMutex(ScriptIsolationLevel isolationLevel, Action<string> taskLog, TaskLock taskLock, CancellationToken cancellationToken, TimeSpan mutexAcquireTimeout, string lockName, string taskId)
        {
            systemLog = Log.System();

            this.isolationLevel = isolationLevel;
            this.taskLog = taskLog;
            this.taskLock = taskLock;
            this.cancellationToken = cancellationToken;
            this.lockName = lockName;
            this.taskId = taskId;
            this.mutexAcquireTimeout = mutexAcquireTimeout;
            lockType = isolationLevel == ScriptIsolationLevel.FullIsolation ? "Write Lock" : "Read Lock";
        }

        IDisposable EnterLock()
        {
            WriteToSystemLog("Trying to acquire lock.");

            switch (isolationLevel)
            {
                case ScriptIsolationLevel.FullIsolation:
                    EnterWriteLock();
                    break;
                case ScriptIsolationLevel.NoIsolation:
                    EnterReadLock();
                    break;
                default:
                    throw new NotSupportedException("Unknown isolation level: " + isolationLevel);
            }

            return this;
        }

        void IDisposable.Dispose()
        {
            WriteToSystemLog("Releasing lock.");
            if (isolationLevel == ScriptIsolationLevel.FullIsolation)
            {
                taskLock.TaskId = null;
            }
            lockReleaser.Dispose();
        }

        void EnterWriteLock()
        {
            if (taskLock.AsyncReaderWriterLock.TryEnterWriterLock(InitialWaitTime, cancellationToken, out lockReleaser))
            {
                taskLock.TaskId = taskId;
                WriteToSystemLog("Lock taken.");
                return;
            }

            WriteToSystemLog($"Failed to acquire lock within {InitialWaitTime}.");

            Busy();

            EnterWriteLockWithTimeout();
        }

        void EnterReadLock()
        {
            if (taskLock.AsyncReaderWriterLock.TryEnterReadLock(InitialWaitTime, cancellationToken, out lockReleaser))
            {
                WriteToSystemLog("Lock taken.");
                return;
            }

            WriteToSystemLog($"Failed to acquire lock within {InitialWaitTime}.");

            Busy();

            EnterReadLockWithTimeout();
        }

        void EnterReadLockWithTimeout()
        {
            WriteToSystemLog($"Trying to acquire lock with wait time of {mutexAcquireTimeout}.");

            if (taskLock.AsyncReaderWriterLock.TryEnterReadLock(mutexAcquireTimeout, cancellationToken, out lockReleaser))
            {
                WriteToSystemLog("Lock taken.");
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                WriteToSystemLog("Lock acquire canceled.");

                Canceled();
                throw new OperationCanceledException(cancellationToken);
            }

            WriteToSystemLog($"Failed to acquire lock within {mutexAcquireTimeout}.");

            TimedOut(mutexAcquireTimeout);
            throw new TimeoutException($"Could not acquire read mutex within timeout {mutexAcquireTimeout}.");
        }

        void EnterWriteLockWithTimeout()
        {
            WriteToSystemLog($"Trying to acquire lock with wait time of {mutexAcquireTimeout}.");

            if (taskLock.AsyncReaderWriterLock.TryEnterWriterLock(mutexAcquireTimeout, cancellationToken, out lockReleaser))
            {
                taskLock.TaskId = taskId;
                WriteToSystemLog("Lock taken.");
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                WriteToSystemLog("Lock acquire canceled.");

                Canceled();
                throw new OperationCanceledException(cancellationToken);
            }

            WriteToSystemLog($"Failed to acquire lock within {mutexAcquireTimeout}.");

            TimedOut(mutexAcquireTimeout);
            throw new TimeoutException($"Could not acquire write mutex within timeout {mutexAcquireTimeout}.");
        }

        void Busy()
        {
            taskLog($"Cannot start this task yet because {taskLock.TaskId ?? "another"} task is currently running and cannot be run in conjunction with any other task. Please wait...");
        }

        void Canceled()
        {
            taskLog("This task was canceled before it could start. The other task is still running.");
        }

        void TimedOut(TimeSpan timeout)
        {
            taskLog($"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. {taskLock.TaskId ?? "Another"} task is still running.");
        }

        void WriteToSystemLog(string message)
        {
            var lockTaken = taskLock.TaskId != null ? $" [Lock taken by {taskLock.TaskId}]" : String.Empty;
            systemLog.Info($"[{taskId}] [{lockName}] [{lockType}]{lockTaken} {message}");
        }

        class TaskLock
        {
            public TaskLock()
            {
                AsyncReaderWriterLock = new AsyncReaderWriterLock();
            }

            public string TaskId { get; set; }

            public AsyncReaderWriterLock AsyncReaderWriterLock { get; }
        }
    }
}