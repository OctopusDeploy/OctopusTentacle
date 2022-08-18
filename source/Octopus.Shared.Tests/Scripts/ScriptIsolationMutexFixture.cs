using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Contracts;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Scripts;
using Octopus.Shared.Tests.Support;

namespace Octopus.Shared.Tests.Scripts
{
    [TestFixture]
    public class ScriptIsolationMutexFixture
    {
        [TestFixture]
        public class TaskLockFixture
        {
            CancellationToken oneSecondCancellationToken;

            [SetUp]
            public void SetUp()
            {
                oneSecondCancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token;
            }

            [Test]
            public void MessagesWhenBlockedDueToAWriteLockAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var lock1 = taskLock.TryEnterWriteLock("ServerTask-1", oneSecondCancellationToken);
                var lock2 = taskLock.TryEnterWriteLock("ServerTask-2", oneSecondCancellationToken);
                lock1.Acquired.Should().BeTrue("Should have got a write lock on a brand new mutex");
                lock2.Acquired.Should().BeFalse("Should not have got a write lock when there was already a write lock");

                taskLock.GetBusyMessage("ServerTask-2", true).Should().Be("Waiting for the script in task [ServerTask-1](~/app#/tasks/ServerTask-1) to finish as this script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-2").Should().Be("This task waited more than 15 minutes and timed out. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                taskLock.GetCanceledMessage("ServerTask-2").Should().Be("This task was canceled before it could start. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                lock1.LockReleaser?.Dispose();
                lock2.LockReleaser?.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToAReadLockAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var lock1 = taskLock.TryEnterReadLock("ServerTask-1", oneSecondCancellationToken);
                var lock2 = taskLock.TryEnterWriteLock("ServerTask-2", oneSecondCancellationToken);
                lock1.Acquired.Should().BeTrue("Should have got a read lock on a brand new mutex");
                lock2.Acquired.Should().BeFalse("Should not have got a write lock when there was already a read lock");

                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-2").Should().Be("This task waited more than 15 minutes and timed out. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                taskLock.GetBusyMessage("ServerTask-2", true).Should().Be("Waiting for the script in task [ServerTask-1](~/app#/tasks/ServerTask-1) to finish as this script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetCanceledMessage("ServerTask-2").Should().Be("This task was canceled before it could start. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                lock1.LockReleaser?.Dispose();
                lock2.LockReleaser?.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToTwoReadLocksAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var lock1 = taskLock.TryEnterReadLock("ServerTask-1", oneSecondCancellationToken);
                var lock2 = taskLock.TryEnterReadLock("ServerTask-2", oneSecondCancellationToken);
                var lock3 = taskLock.TryEnterWriteLock("ServerTask-3", oneSecondCancellationToken);
                lock1.Acquired.Should().BeTrue("Should have got a read lock on a brand new mutex");
                lock2.Acquired.Should().BeTrue("Should have got a second read lock");
                lock3.Acquired.Should().BeFalse("Should not have got a write lock when there was already 2 read locks");

                taskLock.GetBusyMessage("ServerTask-3", true).Should().Be("Waiting on scripts in tasks [ServerTask-1](~/app#/tasks/ServerTask-1) and [ServerTask-2](~/app#/tasks/ServerTask-2) to finish. This script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-3").Should().Be("This task waited more than 15 minutes and timed out. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) and [ServerTask-2](~/app#/tasks/ServerTask-2) are still running.");
                taskLock.GetCanceledMessage("ServerTask-3").Should().Be("This task was canceled before it could start. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) and [ServerTask-2](~/app#/tasks/ServerTask-2) are still running.");
                lock1.LockReleaser?.Dispose();
                lock2.LockReleaser?.Dispose();
                lock3.LockReleaser?.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToMultipleReadLocksAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var lock1 = taskLock.TryEnterReadLock("ServerTask-1", oneSecondCancellationToken);
                var lock2 = taskLock.TryEnterReadLock("ServerTask-2", oneSecondCancellationToken);
                var lock3 = taskLock.TryEnterReadLock("ServerTask-3", oneSecondCancellationToken);
                var lock4 = taskLock.TryEnterWriteLock("ServerTask-4", oneSecondCancellationToken);
                lock1.Acquired.Should().BeTrue("Should have got a read lock on a brand new mutex");
                lock2.Acquired.Should().BeTrue("Should have got a second read lock");
                lock3.Acquired.Should().BeTrue("Should have got a third read lock");
                lock4.Acquired.Should().BeFalse("Should not have got a write lock when there was already 3 read locks");

                taskLock.GetBusyMessage("ServerTask-4", true).Should().Be("Waiting on scripts in tasks [ServerTask-1](~/app#/tasks/ServerTask-1), [ServerTask-2](~/app#/tasks/ServerTask-2) and [ServerTask-3](~/app#/tasks/ServerTask-3) to finish. This script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-4").Should().Be("This task waited more than 15 minutes and timed out. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1), [ServerTask-2](~/app#/tasks/ServerTask-2) and [ServerTask-3](~/app#/tasks/ServerTask-3) are still running.");
                taskLock.GetCanceledMessage("ServerTask-4").Should().Be("This task was canceled before it could start. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1), [ServerTask-2](~/app#/tasks/ServerTask-2) and [ServerTask-3](~/app#/tasks/ServerTask-3) are still running.");
                lock1.LockReleaser?.Dispose();
                lock2.LockReleaser?.Dispose();
                lock3.LockReleaser?.Dispose();
                lock4.LockReleaser?.Dispose();
            }

            [Test]
            public void ReportsWhenMultipleLocksTakenAndReleasedBySameTaskAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var lock1 = taskLock.TryEnterReadLock("ServerTask-1", oneSecondCancellationToken);
                var lock2 = taskLock.TryEnterReadLock("ServerTask-1", oneSecondCancellationToken);

                taskLock.RemoveLock("ServerTask-1");
                taskLock.Report().Should().Be("\"ServerTask-1\" (has a read lock)");

                taskLock.RemoveLock("ServerTask-1");
                taskLock.Report().Should().Be("no locks");

                lock1.LockReleaser?.Dispose();
                lock2.LockReleaser?.Dispose();
            }

            [Test]
            public void ReportsLockHolderChanges()
            {
                var task3Log = new InMemoryLog();
                ScriptIsolationMutex.SubsequentWaitTime = TimeSpan.FromMilliseconds(100);
                var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

                IDisposable AcquireMutex(string taskId, ILog log) => ScriptIsolationMutex.Acquire(ScriptIsolationLevel.FullIsolation,
                    TimeSpan.FromDays(1),
                    nameof(ReportsLockHolderChanges),
                    s => log?.Write(LogCategory.Info, s),
                    taskId,
                    timeoutToken,
                    new TestConsoleLog());

                var firstMutexAcquired = new ManualResetEvent(false);
                var releaseFirstMutex = new ManualResetEvent(false);
                var releaseSecondMutex = new ManualResetEvent(false);
                var t1 = Task.Run(() =>
                    {
                        using var mutex = AcquireMutex("Task-1", null);
                        firstMutexAcquired.Set();
                        releaseFirstMutex.WaitOne();
                    },
                    timeoutToken);
                var t2 = Task.Run(() =>
                    {
                        firstMutexAcquired.WaitOne();
                        using var mutex = AcquireMutex("Task-2", null);
                        releaseSecondMutex.WaitOne();
                    },
                    timeoutToken);
                firstMutexAcquired.WaitOne();
                var t3 = Task.Run(() =>
                    {
                        using var mutex = AcquireMutex("Task-3", task3Log);
                    },
                    timeoutToken);

                task3Log.AssertEventuallyContains("Waiting for the script in task [Task-1]", timeoutToken);
                releaseFirstMutex.Set();
                task3Log.AssertEventuallyContains("Waiting for the script in task [Task-2]", timeoutToken);
                releaseSecondMutex.Set();
                Task.WaitAll(new [] {t1, t2, t3}, timeoutToken);
            }

            [Test]
            public void AcquireCanBeCancelled()
            {
                var cancellationToken = new CancellationTokenSource();

                IDisposable AcquireMutex() => ScriptIsolationMutex.Acquire(ScriptIsolationLevel.FullIsolation,
                    TimeSpan.FromDays(1),
                    nameof(AcquireCanBeCancelled),
                    _ => { },
                    "Task-1",
                    cancellationToken.Token,
                    new TestConsoleLog());

                using var mutex = AcquireMutex();
                Action acquire = () => AcquireMutex();

                cancellationToken.CancelAfter(TimeSpan.FromSeconds(1));
                acquire.Should().Throw<OperationCanceledException>();
            }

            [Test]
            public void LocksBlockOthersThatShareAName()
            {
                using var lock1 = AcquireNamedLock("Lock 1");
                Action a = () => AcquireNamedLock("Lock 1");
                a.Should().Throw<TimeoutException>();
            }

            [Test]
            public void LocksWithDifferentNamesCanBeHeldAtTheSameTime()
            {
                using var lock1 = AcquireNamedLock("Lock 1");
                using var lock2 = AcquireNamedLock("Lock 2");
            }

            static IDisposable AcquireNamedLock(string name) => ScriptIsolationMutex.Acquire(ScriptIsolationLevel.FullIsolation,
                TimeSpan.FromSeconds(1),
                name,
                s =>
                {
                },
                "Task-1",
                CancellationToken.None,
                new TestConsoleLog());
        }
    }
}