using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class MachineWideMutexFixture
    {
        [Test]
        public void CanAcquireMutex()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var machineWideMutex = new MachineWideMutex();
                using (var letMeIn = machineWideMutex.Acquire(Guid.NewGuid().ToString(), cts.Token))
                {
                    letMeIn.Should().NotBeNull();
                }
            }
        }

        [Test]
        public void MutexShouldPartitionByName()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var machineWideMutex = new MachineWideMutex();
                using (var letMeIn = machineWideMutex.Acquire(Guid.NewGuid().ToString(), cts.Token))
                {
                    using (var meToo = machineWideMutex.Acquire(Guid.NewGuid().ToString(), cts.Token))
                    {
                        // Both tasks should be allowed in their own mutex
                        letMeIn.Should().NotBeNull();
                        meToo.Should().NotBeNull();
                    }
                }
            }
        }

        [Test]
        public void MutexShouldOnlyAllowOne_EvenWithDifferentInstances_AndEvenOnTheSameThread()
        {
            using (var firstCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var firstMutexInstance = new MachineWideMutex();
                using (var letMeIn = firstMutexInstance.Acquire("same", firstCancellation.Token))
                {
                    letMeIn.Should().NotBeNull();

                    using (var secondCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                    {
                        var secondMutexInstance = new MachineWideMutex();
                        Assert.Throws<OperationCanceledException>(() => secondMutexInstance.Acquire("same", secondCancellation.Token));
                    }
                }
            }
        }

        [Test]
        public void CancellingBeforeAcquiringBusyMutex_ShouldThrowImmediately()
        {
            using (var cts = new CancellationTokenSource())
            {
                var systemMutex = new MachineWideMutex();

                using (systemMutex.Acquire("same", cts.Token))
                {
                    cts.Cancel();
                    IDisposable second = null;
                    Assert.Throws<OperationCanceledException>(() => second = systemMutex.Acquire("same", cts.Token));
                    second.Should().BeNull();
                }
            }
        }

        [Test]
        public void CancellingBeforeAcquiringBusyMutex_ShouldThrowEvenAfterSomeTimeHasPassed()
        {
            using (var cts = new CancellationTokenSource(1000))
            {
                var systemMutex = new MachineWideMutex(
                    // Go immediately into the wait loop
                    initialAcquisitionAttemptTimeout: TimeSpan.FromMilliseconds(0),
                    // And make the loop really long between attempts
                    waitBetweenAcquisitionAttempts: TimeSpan.FromDays(1));

                using (systemMutex.Acquire("same", cts.Token))
                {
                    // Wait for the mutex to go into the acquisition loop before cancelling
                    // Not a perfect test, but it simulates a real life scenario
                    cts.CancelAfter(TimeSpan.FromSeconds(1));
                    IDisposable second = null;
                    Assert.Throws<OperationCanceledException>(() => second = systemMutex.Acquire("same", cts.Token));
                    second.Should().BeNull();
                }
            }
        }


        [Test]
        public async Task EveryoneShouldBeAbleToGetInEventually()
        {
            var taskCount = 10;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            sw.Start();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
            {
                var systemMutex = new MachineWideMutex(
                    // Go immediately into the wait loop
                    initialAcquisitionAttemptTimeout: TimeSpan.FromMilliseconds(0),
                    // And make the loop really long between attempts
                    waitBetweenAcquisitionAttempts: TimeSpan.FromMilliseconds(100));

                var data = new List<int>();
                Func<int, Task> work = async i =>
                {
                    // Wait my turn
                    using (systemMutex.Acquire("same", cts.Token))
                    {
                        // Get busy for a while
                        // ReSharper disable once AccessToDisposedClosure
                        await Task.Delay(TimeSpan.FromMilliseconds(300), cts.Token);
                        // And record the fact I was here
                        data.Add(i);
                    }
                };

                await Task.WhenAll(Enumerable.Range(0, taskCount).Select(i => work(i)));
                Console.WriteLine($"data=[{string.Join(",", data)}] in {sw.Elapsed}");
                data.Count.Should().Be(taskCount, "All of the tasks should have had their turn in the mutex.");
            }
        }
    }
}