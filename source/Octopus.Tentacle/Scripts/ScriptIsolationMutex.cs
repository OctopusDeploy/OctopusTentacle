using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nito.AsyncEx;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public static class ScriptIsolationMutex
    {
        // Reader-writer locks allow multiple readers, but only one writer which blocks readers. This is perfect for our scenario, because
        // we want to allow lots of scripts to run with the 'no' isolation level, but nothing should be running under the 'full' isolation level.
        // NOTE: Changed from ReaderWriterLockSlim to AsyncReaderWriterLock to enable cooperative cancellation whilst waiting for the lock.
        //       Hopefully in a future version of .NET there will be a fully supported ReaderWriterLock with cooperative cancellation support so we can remove this dependency.
        static readonly ConcurrentDictionary<string, TaskLock> ReaderWriterLocks = new ConcurrentDictionary<string, TaskLock>();
        static readonly TimeSpan InitialWaitTime = TimeSpan.FromMilliseconds(100);
        internal static TimeSpan SubsequentWaitTime = TimeSpan.FromMinutes(10);

        public static readonly TimeSpan NoTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

        public static IDisposable Acquire(ScriptIsolationLevel isolation,
            TimeSpan mutexAcquireTimeout,
            string lockName,
            Action<string> taskLog,
            string taskId,
            CancellationToken token,
            ILog log)
        {
            taskLog.WriteVerbose($"Acquiring isolation mutex {lockName} with {isolation} in {taskId}");
            var taskLock = ReaderWriterLocks.GetOrAdd(lockName, _ => new TaskLock());

            return new ScriptIsolationMutexReleaser(isolation,
                taskLog,
                taskLock,
                token,
                mutexAcquireTimeout,
                lockName,
                taskId,
                log).EnterLock();
        }

        class ScriptIsolationMutexReleaser : IDisposable
        {
            readonly TimeSpan mutexAcquireTimeout;
            readonly string lockName;
            readonly string taskId;
            readonly ScriptIsolationLevel isolationLevel;
            readonly Action<string> taskLog;
            readonly TaskLock taskLock;
            readonly CancellationToken cancellationToken;
            readonly ILog systemLog;
            readonly string lockType;
            IDisposable? lockReleaser;

            public ScriptIsolationMutexReleaser(ScriptIsolationLevel isolationLevel,
                Action<string> taskLog,
                TaskLock taskLock,
                CancellationToken cancellationToken,
                TimeSpan mutexAcquireTimeout,
                string lockName,
                string taskId,
                ILog log)
            {
                systemLog = log;

                this.isolationLevel = isolationLevel;
                this.taskLog = taskLog;
                this.taskLock = taskLock;
                this.cancellationToken = cancellationToken;
                this.lockName = lockName;
                this.taskId = taskId;
                this.mutexAcquireTimeout = mutexAcquireTimeout;
                lockType = isolationLevel == ScriptIsolationLevel.FullIsolation ? "Write Lock" : "Read Lock";
            }

            public IDisposable EnterLock()
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
                taskLock.RemoveLock(taskId);
                lockReleaser?.Dispose();
            }

            void EnterWriteLock() => PollForLock(taskLock.TryEnterWriteLock);

            void EnterReadLock() => PollForLock(taskLock.TryEnterReadLock);

            void PollForLock(Func<string, CancellationToken, AcquireLockResult> acquireLock)
            {
                WriteToSystemLog($"Trying to acquire lock with wait time of {mutexAcquireTimeout}.");
                var pollingInterval = InitialWaitTime;

                using var timeoutSource = new CancellationTokenSource(mutexAcquireTimeout);
                while (!cancellationToken.IsCancellationRequested && !timeoutSource.IsCancellationRequested)
                {
                    using var pollingIntervalSource = new CancellationTokenSource(pollingInterval);
                    using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        timeoutSource.Token,
                        pollingIntervalSource.Token);
                    var result = acquireLock(taskId, linkedCancellationTokenSource.Token);
                    if (result.Acquired)
                    {
                        lockReleaser = result.LockReleaser;
                        WriteToSystemLog("Lock acquired.");
                        return;
                    }

                    Busy();
                    pollingInterval = SubsequentWaitTime;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    WriteToSystemLog("Lock acquire canceled.");

                    Canceled();
                    throw new OperationCanceledException(cancellationToken);
                }

                if (timeoutSource.IsCancellationRequested)
                {
                    WriteToSystemLog($"Failed to acquire lock within {mutexAcquireTimeout}.");

                    TimedOut(mutexAcquireTimeout);
                    throw new TimeoutException($"Could not acquire read mutex within timeout {mutexAcquireTimeout}.");
                }
            }

            void Busy()
            {
                taskLog.WriteWait(taskLock.GetBusyMessage(taskId, isolationLevel == ScriptIsolationLevel.FullIsolation));
            }

            void Canceled()
            {
                taskLog(taskLock.GetCanceledMessage(taskId));
            }

            void TimedOut(TimeSpan timeout)
            {
                taskLog(taskLock.GetTimedOutMessage(timeout, taskId));
            }

            void WriteToSystemLog(string message)
            {
                var lockTaken = $" [{taskLock.Report()}]";
                systemLog.Trace($"[{taskId}] [{lockName}] [{lockType}]{lockTaken} {message}");
            }
        }

        internal class TaskLock
        {
            readonly object stateLock = new();
            readonly IDictionary<string, int> readersTaskIds = new Dictionary<string, int>();
            readonly AsyncReaderWriterLock asyncReaderWriterLock;
            string? writerTaskId;

            public TaskLock()
            {
                asyncReaderWriterLock = new AsyncReaderWriterLock();
            }

            public AcquireLockResult TryEnterWriteLock(string taskId, CancellationToken cancellationToken)
            {
                var result = asyncReaderWriterLock.TryEnterWriteLock(cancellationToken);
                if (!result.Acquired)
                {
                    return result;
                }

                writerTaskId = taskId;
                return result;
            }

            public AcquireLockResult TryEnterReadLock(string taskId, CancellationToken cancellationToken)
            {
                var result = asyncReaderWriterLock.TryEnterReadLock(cancellationToken);
                if (!result.Acquired)
                {
                    return result;
                }

                lock (stateLock)
                {
                    if (readersTaskIds.ContainsKey(taskId))
                    {
                        readersTaskIds[taskId]++;
                    }
                    else
                    {
                        readersTaskIds[taskId] = 1;
                    }
                }

                return result;
            }

            public void RemoveLock(string taskId)
            {
                if (writerTaskId == taskId)
                {
                    writerTaskId = null;
                    return;
                }

                lock (stateLock)
                {
                    if (!readersTaskIds.ContainsKey(taskId))
                    {
                        return;
                    }

                    if (readersTaskIds[taskId] > 1)
                    {
                        readersTaskIds[taskId]--;
                        return;
                    }

                    readersTaskIds.Remove(taskId);
                }
            }

            public string Report()
            {
                if (writerTaskId != null)
                {
                    return $"\"{writerTaskId}\" (has a write lock)";
                }

                lock (stateLock)
                {
                    var ids = readersTaskIds.Keys.ToArray();

                    if (ids.Length == 0)
                    {
                        return "no locks";
                    }

                    var readerTaskIds = string.Join(", ", ids);

                    var result = $"\"{readerTaskIds}\"";

                    if (ids.Length > 1)
                        result += " (have read locks)";
                    else
                        result += " (has a read lock)";

                    return result;
                }
            }

            (string message, bool multiple, bool thisTaskAlreadyHasLock) ListTasksWithMarkdownLinks(string taskId)
            {
                var localWriterTaskId = writerTaskId; // This could change during the execution of the method
                if (localWriterTaskId != null)
                    return ($"[{localWriterTaskId}](~/app#/tasks/{localWriterTaskId})", false, localWriterTaskId == taskId);

                lock (stateLock)
                {
                    var ids = readersTaskIds.Keys.OrderBy(x => x).ToArray();

                    var message = ids.Any()
                        ? ids.Select(x => x == taskId ? "This Task" : $"[{x}](~/app#/tasks/{x})").ReadableJoin()
                        : "(error - task not found)";

                    return (message, ids.Length > 1, ids.Contains(taskId));
                }
            }

            public string GetBusyMessage(string taskId, bool isWaitingOnWrite)
            {
                var (message, multiple, thisTaskAlreadyHasLock) = ListTasksWithMarkdownLinks(taskId);

                if (multiple) // Waiting on multiple, that means they are all read and this is a write
                {
                    return $"Waiting on scripts in tasks {message} to finish. This script requires that no other Octopus scripts are executing on this target at the same time.";
                }

                if (thisTaskAlreadyHasLock)
                {
                    return $"Waiting on another script in this task to finish as {(isWaitingOnWrite ? "this" : "another")} task requires that no other Octopus scripts are executing on this target at the same time.";
                }

                return $"Waiting for the script in task {message} to finish as {(isWaitingOnWrite ? "this" : "that")} script requires that no other Octopus scripts are executing on this target at the same time.";
            }

            public string GetCanceledMessage(string taskId)
            {
                var (message, multiple, _) = ListTasksWithMarkdownLinks(taskId);

                return multiple
                    ? $"This task was canceled before it could start. Tasks {message} are still running."
                    : $"This task was canceled before it could start. Task {message} is still running.";
            }

            public string GetTimedOutMessage(TimeSpan timeout, string taskId)
            {
                var (message, multiple, _) = ListTasksWithMarkdownLinks(taskId);

                return multiple
                    ? $"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. Tasks {message} are still running."
                    : $"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. Task {message} is still running.";
            }
        }
    }
}