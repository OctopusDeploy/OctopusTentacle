using System;
using System.Threading;
using FluentAssertions;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Shared.Contracts;
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
            [Test]
            public void MessagesWhenBlockedDueToAWriteLockAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var gotLock1 = taskLock.TryEnterWriteLock("ServerTask-1", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser);
                var gotLock2 = taskLock.TryEnterWriteLock("ServerTask-2", TimeSpan.FromSeconds(1), CancellationToken.None, out _);
                gotLock1.Should().BeTrue("Should have got a write lock on a brand new mutex");
                gotLock2.Should().BeFalse("Should not have got a write lock when there was already a write lock");

                taskLock.GetBusyMessage("ServerTask-2", true).Should().Be("Waiting for the script in task [ServerTask-1](~/app#/tasks/ServerTask-1) to finish as this script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-2").Should().Be("This task waited more than 15 minutes and timed out. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                taskLock.GetCanceledMessage("ServerTask-2").Should().Be("This task was canceled before it could start. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                lockReleaser.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToAReadLockAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var gotLock1 = taskLock.TryEnterReadLock("ServerTask-1", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser);
                var gotLock2 = taskLock.TryEnterWriteLock("ServerTask-2", TimeSpan.FromSeconds(1), CancellationToken.None, out _);
                gotLock1.Should().BeTrue("Should have got a read lock on a brand new mutex");
                gotLock2.Should().BeFalse("Should not have got a write lock when there was already a read lock");

                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-2").Should().Be("This task waited more than 15 minutes and timed out. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                taskLock.GetBusyMessage("ServerTask-2", true).Should().Be("Waiting for the script in task [ServerTask-1](~/app#/tasks/ServerTask-1) to finish as this script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetCanceledMessage("ServerTask-2").Should().Be("This task was canceled before it could start. Task [ServerTask-1](~/app#/tasks/ServerTask-1) is still running.");
                lockReleaser.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToTwoReadLocksAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var gotLock1 = taskLock.TryEnterReadLock("ServerTask-1", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser);
                var gotLock2 = taskLock.TryEnterReadLock("ServerTask-2", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser2);
                var gotLock3 = taskLock.TryEnterWriteLock("ServerTask-3", TimeSpan.FromSeconds(1), CancellationToken.None, out _);
                gotLock1.Should().BeTrue("Should have got a read lock on a brand new mutex");
                gotLock2.Should().BeTrue("Should have got a second read lock");
                gotLock3.Should().BeFalse("Should not have got a write lock when there was already 2 read locks");

                taskLock.GetBusyMessage("ServerTask-3", true).Should().Be("Waiting on scripts in tasks [ServerTask-1](~/app#/tasks/ServerTask-1) and [ServerTask-2](~/app#/tasks/ServerTask-2) to finish. This script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-3").Should().Be("This task waited more than 15 minutes and timed out. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) and [ServerTask-2](~/app#/tasks/ServerTask-2) are still running.");
                taskLock.GetCanceledMessage("ServerTask-3").Should().Be("This task was canceled before it could start. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) and [ServerTask-2](~/app#/tasks/ServerTask-2) are still running.");
                lockReleaser.Dispose();
                lockReleaser2.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToMultipleReadLocksAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var gotLock1 = taskLock.TryEnterReadLock("ServerTask-1", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser);
                var gotLock2 = taskLock.TryEnterReadLock("ServerTask-2", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser2);
                var gotLock3 = taskLock.TryEnterReadLock("ServerTask-3", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser3);
                var gotLock4 = taskLock.TryEnterWriteLock("ServerTask-4", TimeSpan.FromSeconds(1), CancellationToken.None, out _);
                gotLock1.Should().BeTrue("Should have got a read lock on a brand new mutex");
                gotLock2.Should().BeTrue("Should have got a second read lock");
                gotLock3.Should().BeTrue("Should have got a third read lock");
                gotLock4.Should().BeFalse("Should not have got a write lock when there was already 3 read locks");

                taskLock.GetBusyMessage("ServerTask-4", true).Should().Be("Waiting on scripts in tasks [ServerTask-1](~/app#/tasks/ServerTask-1), [ServerTask-2](~/app#/tasks/ServerTask-2) and [ServerTask-3](~/app#/tasks/ServerTask-3) to finish. This script requires that no other Octopus scripts are executing on this target at the same time.");
                taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15), "ServerTask-4").Should().Be("This task waited more than 15 minutes and timed out. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1), [ServerTask-2](~/app#/tasks/ServerTask-2) and [ServerTask-3](~/app#/tasks/ServerTask-3) are still running.");
                taskLock.GetCanceledMessage("ServerTask-4").Should().Be("This task was canceled before it could start. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1), [ServerTask-2](~/app#/tasks/ServerTask-2) and [ServerTask-3](~/app#/tasks/ServerTask-3) are still running.");
                lockReleaser.Dispose();
                lockReleaser2.Dispose();
                lockReleaser3.Dispose();
            }

            [Test]
            public void LocksBlockOthersThatShareAName()
            {
                var lock1 = AcquireNamedLock("Lock 1");
                Action a = () => AcquireNamedLock("Lock 1");
                a.Should().Throw<TimeoutException>();
                lock1.Dispose();
            }

            [Test]
            public void LocksWithDifferentNamesCanBeHeldAtTheSameTime()
            {
                var lock1 = AcquireNamedLock("Lock 1");
                var lock2 = AcquireNamedLock("Lock 2");
                lock1.Dispose();
                lock2.Dispose();
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