using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Nito.AsyncEx;
using Octopus.Diagnostics;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class ScriptIsolationMutex
    {
        // Reader-writer locks allow multiple readers, but only one writer which blocks readers. This is perfect for our scenario, because
        // we want to allow lots of scripts to run with the 'no' isolation level, but nothing should be running under the 'full' isolation level.
        // NOTE: Changed from ReaderWriterLockSlim to AsyncReaderWriterLock to enable cooperative cancellation whilst waiting for the lock.
        //       Hopefully in a future version of .NET there will be a fully supported ReaderWriterLock with cooperative cancellation support so we can remove this dependency.
        static readonly ConcurrentDictionary<string, TaskLock> ReaderWriterLocks = new ConcurrentDictionary<string, TaskLock>();
        static readonly TimeSpan InitialWaitTime = TimeSpan.FromMilliseconds(100);

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

            void EnterWriteLock()
            {
                if (taskLock.TryEnterWriteLock(taskId, InitialWaitTime, cancellationToken, out lockReleaser))
                {
                    WriteToSystemLog("Lock taken.");
                    return;
                }

                WriteToSystemLog($"Failed to acquire lock within {InitialWaitTime}.");

                Busy(true);

                EnterWriteLockWithTimeout();
            }

            void EnterReadLock()
            {
                if (taskLock.TryEnterReadLock(taskId, InitialWaitTime, cancellationToken, out lockReleaser))
                {
                    WriteToSystemLog("Lock taken.");
                    return;
                }

                WriteToSystemLog($"Failed to acquire lock within {InitialWaitTime}.");

                Busy(false);

                EnterReadLockWithTimeout();
            }

            void EnterReadLockWithTimeout()
            {
                WriteToSystemLog($"Trying to acquire lock with wait time of {mutexAcquireTimeout}.");

                if (taskLock.TryEnterReadLock(taskId, mutexAcquireTimeout, cancellationToken, out lockReleaser))
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

                if (taskLock.TryEnterWriteLock(taskId, mutexAcquireTimeout, cancellationToken, out lockReleaser))
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
                throw new TimeoutException($"Could not acquire write mutex within timeout {mutexAcquireTimeout}.");
            }

            void Busy(bool isWaitingOnWrite)
            {
                taskLog.WriteWait(taskLock.GetBusyMessage(taskId, isWaitingOnWrite));
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
            readonly ConcurrentDictionary<string, object?> readersTaskIds = new ConcurrentDictionary<string, object?>();
            readonly AsyncReaderWriterLock asyncReaderWriterLock;
            string? writerTaskId;

            public TaskLock()
            {
                asyncReaderWriterLock = new AsyncReaderWriterLock();
            }

            public bool TryEnterWriteLock(string taskId, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable? releaseLock)
            {
                if (asyncReaderWriterLock.TryEnterWriteLock(timeout, cancellationToken, out releaseLock))
                {
                    writerTaskId = taskId;
                    return true;
                }

                return false;
            }

            public bool TryEnterReadLock(string taskId, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable? releaseLock)
            {
                if (asyncReaderWriterLock.TryEnterReadLock(timeout, cancellationToken, out releaseLock))
                {
                    readersTaskIds.TryAdd(taskId, null);
                    return true;
                }

                return false;
            }

            public void RemoveLock(string taskId)
            {
                if (writerTaskId == taskId)
                {
                    writerTaskId = null;
                    return;
                }

                readersTaskIds.TryRemove(taskId, out var _);
            }

            public string Report()
            {
                string result;

                if (writerTaskId != null)
                {
                    result = $"\"{writerTaskId}\" (has a write lock)";
                }
                else
                {
                    var ids = readersTaskIds.Keys.ToArray();

                    if (ids.Length == 0)
                        return "no locks";

                    var readerTaskIds = string.Join(", ", ids);

                    result = $"\"{readerTaskIds}\"";

                    if (ids.Length > 1)
                        result += " (have read locks)";
                    else
                        result += " (has a read lock)";
                }

                return result;
            }

            (string message, bool multiple, bool thisTaskAlreadyHasLock) ListTasksWithMarkdownLinks(string taskId)
            {
                var localWriterTaskId = writerTaskId; // This could change during the execution of the method
                if (localWriterTaskId != null)
                    return ($"[{localWriterTaskId}](~/app#/tasks/{localWriterTaskId})", false, localWriterTaskId == taskId);

                var ids = readersTaskIds.Keys.OrderBy(x => x).ToArray();

                var message = ids.Any()
                    ? ids.Select(x => x == taskId ? "This Task" : $"[{x}](~/app#/tasks/{x})").ReadableJoin()
                    : "(error - task not found)";

                return (message, ids.Length > 1, ids.Contains(taskId));
            }

            public string GetBusyMessage(string taskId, bool isWaitingOnWrite)
            {
                var listTasksWithMarkdownLinks = ListTasksWithMarkdownLinks(taskId);

                if (listTasksWithMarkdownLinks.multiple) // Waiting on multiple, that means they are all read and this is a write
                    return $"Waiting on scripts in tasks {listTasksWithMarkdownLinks.message} to finish. This script requires that no other Octopus scripts are executing on this target at the same time.";

                if (listTasksWithMarkdownLinks.thisTaskAlreadyHasLock)
                    return $"Waiting on another script in this task to finish as {(isWaitingOnWrite ? "this" : "another")} task requires that no other Octopus scripts are executing on this target at the same time.";

                return $"Waiting for the script in task {listTasksWithMarkdownLinks.message} to finish as {(isWaitingOnWrite ? "this" : "that")} script requires that no other Octopus scripts are executing on this target at the same time.";
            }

            public string GetCanceledMessage(string taskId)
            {
                var listTasksWithMarkdownLinks = ListTasksWithMarkdownLinks(taskId);

                return listTasksWithMarkdownLinks.multiple
                    ? $"This task was canceled before it could start. Tasks {listTasksWithMarkdownLinks.message} are still running."
                    : $"This task was canceled before it could start. Task {listTasksWithMarkdownLinks.message} is still running.";
            }

            public string GetTimedOutMessage(TimeSpan timeout, string taskId)
            {
                var listTasksWithMarkdownLinks = ListTasksWithMarkdownLinks(taskId);

                return listTasksWithMarkdownLinks.multiple
                    ? $"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. Tasks {listTasksWithMarkdownLinks.message} are still running."
                    : $"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. Task {listTasksWithMarkdownLinks.message} is still running.";
            }
        }
    }
}