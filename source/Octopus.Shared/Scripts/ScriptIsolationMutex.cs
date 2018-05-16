using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Nito.AsyncEx;
using Octopus.Shared.Contracts;
using Octopus.Shared.Diagnostics;
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

        public static IDisposable Acquire(ScriptIsolationLevel isolation, TimeSpan mutexAcquireTimeout, string lockName, Action<string> taskLog, string taskId, CancellationToken token)
        {
            var taskLock = ReaderWriterLocks.GetOrAdd(lockName, _ => new TaskLock());

            return new ScriptIsolationMutexReleaser(isolation, taskLog, taskLock, token, mutexAcquireTimeout, lockName, taskId).EnterLock();
        }

        class ScriptIsolationMutexReleaser: IDisposable
        {
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

            public ScriptIsolationMutexReleaser(ScriptIsolationLevel isolationLevel, Action<string> taskLog, TaskLock taskLock, CancellationToken cancellationToken, TimeSpan mutexAcquireTimeout, string lockName, string taskId)
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
                lockReleaser.Dispose();
            }

            void EnterWriteLock()
            {
                if (taskLock.TryEnterWriteLock(taskId, InitialWaitTime, cancellationToken, out lockReleaser))
                {
                    WriteToSystemLog("Lock taken.");
                    return;
                }

                WriteToSystemLog($"Failed to acquire lock within {InitialWaitTime}.");

                Busy();

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

                Busy();

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

            void Busy()
            {
                taskLog.WriteWait(taskLock.GetBusyMessage());
            }

            void Canceled()
            {
                taskLog(taskLock.GetCanceledMessage());
            }

            void TimedOut(TimeSpan timeout)
            {
                taskLog(taskLock.GetTimedOutMessage(timeout));
            }

            void WriteToSystemLog(string message)
            {
                var lockTaken = $" [{taskLock.Report()}]";
                systemLog.Info($"[{taskId}] [{lockName}] [{lockType}]{lockTaken} {message}");
            }
        }

        internal class TaskLock
        {
            readonly ConcurrentDictionary<string, object> readersTaskIds = new ConcurrentDictionary<string, object>();
            readonly AsyncReaderWriterLock asyncReaderWriterLock;
            string writerTaskId;

            public TaskLock()
            {
                asyncReaderWriterLock = new AsyncReaderWriterLock();
            }

            public bool TryEnterWriteLock(string taskId, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable releaseLock)
            {
                if (asyncReaderWriterLock.TryEnterWriteLock(timeout, cancellationToken, out releaseLock))
                {
                    writerTaskId = taskId;
                    return true;
                }

                return false;
            }

            public bool TryEnterReadLock(string taskId, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable releaseLock)
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

                object _;
                readersTaskIds.TryRemove(taskId, out _);
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
                    {
                        return "no locks";
                    }

                    var readerTaskIds = String.Join(", ", ids);
                    
                    result = $"\"{readerTaskIds}\"";

                    if (ids.Length > 1)
                    {
                        result += " (have read locks)";
                    }
                    else
                    {
                        result += " (has a read lock)";
                    }
                }

                return result;
            }

            private string ListTasksWithMarkdownLinks()
            {
                if (writerTaskId != null)
                {
                    return $"[{writerTaskId}](~/app#/tasks/{writerTaskId}) (RW)";
                }

                var ids = readersTaskIds.Keys.OrderBy(x => x).ToArray();

                return ids.Any() 
                    ? ids.Select(x => $"[{x}](~/app#/tasks/{x}) (R)").ReadableJoin() 
                    : "(error - task not found)";
            }

            private bool IsWaitingOnMultipleTasks()
            {
                if (writerTaskId != null)
                    return false;
                return readersTaskIds.Keys.Count > 1;
            }

            public string GetBusyMessage()
            {
                return IsWaitingOnMultipleTasks() 
                    ? $"Cannot start this task yet because tasks {ListTasksWithMarkdownLinks()} are currently running and this task cannot be run in conjunction with any other tasks. Please wait..."
                    : $"Cannot start this task yet because task {ListTasksWithMarkdownLinks()} is currently running and this task cannot be run in conjunction with any other tasks. Please wait...";
            }

            public string GetCanceledMessage()
            {
                return IsWaitingOnMultipleTasks() 
                    ? $"This task was canceled before it could start. Tasks {ListTasksWithMarkdownLinks()} are still running."
                    : $"This task was canceled before it could start. Task {ListTasksWithMarkdownLinks()} is still running.";
            }

            public string GetTimedOutMessage(TimeSpan timeout)
            {
                return IsWaitingOnMultipleTasks()
                    ? $"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. Tasks {ListTasksWithMarkdownLinks()} are still running."
                    : $"This task waited more than {timeout.TotalMinutes:N0} minutes and timed out. Task {ListTasksWithMarkdownLinks()} is still running.";
            }
        }
    }
}