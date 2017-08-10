using System;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Tests.Support;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class OctoMonitorFixture
    {
        InMemoryLog log;
        InMemoryLog systemLog;

        public override string ToString() => nameof(OctoMonitorFixture);

        [SetUp]
        public void SetUp()
        {
            OctoMonitor.Log = log = new InMemoryLog();
            OctoMonitor.SystemLog = systemLog = new InMemoryLog();
        }

        [TearDown]
        public void TearDown()
        {
            OctoMonitor.Log = global::Octopus.Shared.Diagnostics.Log.Octopus();
            OctoMonitor.SystemLog = global::Octopus.Shared.Diagnostics.Log.System();
            OctoMonitor.InitialAcquisitionAttemptTimeout = OctoMonitor.DefaultInitialAcquisitionAttemptTimeout;
            OctoMonitor.WaitBetweenAcquisitionAttempts = OctoMonitor.DefaultWaitBetweenAcquisitionAttempts;
        }

        [Test]
        public void CanEnterMonitor()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                using (var letMeIn = OctoMonitor.Enter(this, "waiting", cts.Token))
                {
                    letMeIn.Should().NotBeNull();
                }
            }
        }

        [Test]
        public void CanBeUsedMultipleTimes()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                using (var letMeIn = OctoMonitor.Enter(this, "waiting", cts.Token))
                {
                    letMeIn.Should().NotBeNull();
                }
                using (var letMeInAgain = OctoMonitor.Enter(this, "waiting", cts.Token))
                {
                    letMeInAgain.Should().NotBeNull();
                }
            }
        }

        [Test]
        public void ShouldBeRecursive()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                using (var letMeIn = OctoMonitor.Enter(this, "waiting", cts.Token))
                {
                    letMeIn.Should().NotBeNull();
                    using (var letMeInAgain = OctoMonitor.Enter(this, "waiting", cts.Token))
                    {
                        letMeInAgain.Should().NotBeNull();
                    }
                }
            }
        }

        [Test]
        public void MutexShouldPartitionByObject()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var first = new object();
                var second = new object();
                using (var letMeIn = OctoMonitor.Enter(first, "waiting", cts.Token))
                {
                    using (var meToo = OctoMonitor.Enter(second, "waiting", cts.Token))
                    {
                        // Both tasks should be allowed in their own mutex
                        letMeIn.Should().NotBeNull();
                        meToo.Should().NotBeNull();
                    }
                }
            }
        }

        [Test]
        public void ShouldActuallySynchronize()
        {
            IDisposable letMeIn;
            IDisposable meToo = null;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var wait = new AutoResetEvent(false);
                using (letMeIn = OctoMonitor.Enter(this, "waiting1", cts.Token))
                {
                    var thread = new Thread(() =>
                    {
                        Assert.Throws<OperationCanceledException>(() => meToo = OctoMonitor.Enter(this, "waiting2", cts.Token), "time should have expired before we entered the monitor");
                        wait.Set();
                    });

                    thread.Start();

                    wait.WaitOne(TimeSpan.FromSeconds(5));

                    thread.Join(TimeSpan.FromSeconds(1));
                }
            }

            letMeIn.Should().NotBeNull("we should have entered the mutex immediately");
            meToo.Should().BeNull("we shouldn't have entered the second monitor in time");
        }

        [Test]
        public void ShouldLogWhenWaiting()
        {
            var expectedWaitMessage = "We are the second thread trying to enter the monitor";
            OctoMonitor.InitialAcquisitionAttemptTimeout = TimeSpan.Zero;
            OctoMonitor.WaitBetweenAcquisitionAttempts = TimeSpan.FromMilliseconds(100);

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var wait = new AutoResetEvent(false);
                using (OctoMonitor.Enter(this, "waiting1", cts.Token))
                {
                    var thread = new Thread(() =>
                    {
                        Assert.Throws<OperationCanceledException>(() => OctoMonitor.Enter(this, expectedWaitMessage, cts.Token), "time should have expired before we entered the monitor");
                        wait.Set();
                    });

                    thread.Start();

                    wait.WaitOne(TimeSpan.FromSeconds(5));

                    thread.Join(TimeSpan.FromSeconds(1));
                }
            }

            Console.WriteLine("Task Log");
            Console.WriteLine(log.GetLog());
            Console.WriteLine("System Log");
            Console.WriteLine(systemLog.GetLog());

            log.GetLog().Should().Contain(expectedWaitMessage);
            systemLog.GetLog().Should().Contain($"Verbose Monitor {ToString()} in use, waiting. {expectedWaitMessage}");
        }
    }
}