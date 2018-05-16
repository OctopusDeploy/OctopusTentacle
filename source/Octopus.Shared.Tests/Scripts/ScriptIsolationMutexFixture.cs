using System;
using System.Threading;
using NUnit.Framework;
using Octopus.Shared.Scripts;

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
                Assert.That(gotLock1, Is.True, "Should have got a write lock on a brand new mutex");
                Assert.That(gotLock2, Is.False, "Should not have got a write lock when there was already a write lock");
                
                Assert.That(taskLock.GetBusyMessage(), Is.EqualTo("Cannot start this task yet because task [ServerTask-1](~/app#/tasks/ServerTask-1) (RW) is currently running and this task cannot be run in conjunction with any other tasks. Please wait..."));
                Assert.That(taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15)), Is.EqualTo("This task waited more than 15 minutes and timed out. Task [ServerTask-1](~/app#/tasks/ServerTask-1) (RW) is still running."));
                Assert.That(taskLock.GetCanceledMessage(), Is.EqualTo("This task was canceled before it could start. Task [ServerTask-1](~/app#/tasks/ServerTask-1) (RW) is still running."));
                lockReleaser.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToAReadLockAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var gotLock1 = taskLock.TryEnterReadLock("ServerTask-1", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser);
                var gotLock2 = taskLock.TryEnterWriteLock("ServerTask-2", TimeSpan.FromSeconds(1), CancellationToken.None, out _);
                Assert.That(gotLock1, Is.True, "Should have got a read lock on a brand new mutex");
                Assert.That(gotLock2, Is.False, "Should not have got a write lock when there was already a read lock");
                
                Assert.That(taskLock.GetBusyMessage(), Is.EqualTo("Cannot start this task yet because task [ServerTask-1](~/app#/tasks/ServerTask-1) (R) is currently running and this task cannot be run in conjunction with any other tasks. Please wait..."));
                Assert.That(taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15)), Is.EqualTo("This task waited more than 15 minutes and timed out. Task [ServerTask-1](~/app#/tasks/ServerTask-1) (R) is still running."));
                Assert.That(taskLock.GetCanceledMessage(), Is.EqualTo("This task was canceled before it could start. Task [ServerTask-1](~/app#/tasks/ServerTask-1) (R) is still running."));
                lockReleaser.Dispose();
            }

            [Test]
            public void MessagesWhenBlockedDueToTwoReadLocksAreCorrect()
            {
                var taskLock = new ScriptIsolationMutex.TaskLock();
                var gotLock1 = taskLock.TryEnterReadLock("ServerTask-1", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser);
                var gotLock2 = taskLock.TryEnterReadLock("ServerTask-2", TimeSpan.FromSeconds(1), CancellationToken.None, out var lockReleaser2);
                var gotLock3 = taskLock.TryEnterWriteLock("ServerTask-3", TimeSpan.FromSeconds(1), CancellationToken.None, out _);
                Assert.That(gotLock1, Is.True, "Should have got a read lock on a brand new mutex");
                Assert.That(gotLock2, Is.True, "Should have got a second read lock");
                Assert.That(gotLock3, Is.False, "Should not have got a write lock when there was already 2 read locks");
                
                Assert.That(taskLock.GetBusyMessage(), Is.EqualTo("Cannot start this task yet because tasks [ServerTask-1](~/app#/tasks/ServerTask-1) (R) and [ServerTask-2](~/app#/tasks/ServerTask-2) (R) are currently running and this task cannot be run in conjunction with any other tasks. Please wait..."));
                Assert.That(taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15)), Is.EqualTo("This task waited more than 15 minutes and timed out. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) (R) and [ServerTask-2](~/app#/tasks/ServerTask-2) (R) are still running."));
                Assert.That(taskLock.GetCanceledMessage(), Is.EqualTo("This task was canceled before it could start. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) (R) and [ServerTask-2](~/app#/tasks/ServerTask-2) (R) are still running."));
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
                Assert.That(gotLock1, Is.True, "Should have got a read lock on a brand new mutex");
                Assert.That(gotLock2, Is.True, "Should have got a second read lock");
                Assert.That(gotLock3, Is.True, "Should have got a third read lock");
                Assert.That(gotLock4, Is.False, "Should not have got a write lock when there was already 3 read locks");
                
                Assert.That(taskLock.GetBusyMessage(), Is.EqualTo("Cannot start this task yet because tasks [ServerTask-1](~/app#/tasks/ServerTask-1) (R), [ServerTask-2](~/app#/tasks/ServerTask-2) (R) and [ServerTask-3](~/app#/tasks/ServerTask-3) (R) are currently running and this task cannot be run in conjunction with any other tasks. Please wait..."));
                Assert.That(taskLock.GetTimedOutMessage(TimeSpan.FromMinutes(15)), Is.EqualTo("This task waited more than 15 minutes and timed out. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) (R), [ServerTask-2](~/app#/tasks/ServerTask-2) (R) and [ServerTask-3](~/app#/tasks/ServerTask-3) (R) are still running."));
                Assert.That(taskLock.GetCanceledMessage(), Is.EqualTo("This task was canceled before it could start. Tasks [ServerTask-1](~/app#/tasks/ServerTask-1) (R), [ServerTask-2](~/app#/tasks/ServerTask-2) (R) and [ServerTask-3](~/app#/tasks/ServerTask-3) (R) are still running."));
                lockReleaser.Dispose();
                lockReleaser2.Dispose();
                lockReleaser3.Dispose();
            }
        }
    }
}
