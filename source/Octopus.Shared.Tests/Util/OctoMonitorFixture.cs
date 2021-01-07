using System;
using System.Threading;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Tests.Support;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class OctoMonitorFixture
    {
        InMemoryLog systemLog;

        public override string ToString() => nameof(OctoMonitorFixture);

        [SetUp]
        public void SetUp()
        {
            OctoMonitor.SystemLog = systemLog = new InMemoryLog();
        }

        [TearDown]
        public void TearDown()
        {
            OctoMonitor.SystemLog = new SystemLog();
            OctoMonitor.InitialAcquisitionAttemptTimeout = OctoMonitor.DefaultInitialAcquisitionAttemptTimeout;
            OctoMonitor.WaitBetweenAcquisitionAttempts = OctoMonitor.DefaultWaitBetweenAcquisitionAttempts;
        }

        [Test]
        public void CanEnterMonitor()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                using (var letMeIn = OctoMonitor.Enter(this, "waiting", cts.Token, Substitute.For<ILog>()))
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
                using (var letMeIn = OctoMonitor.Enter(this, "waiting", cts.Token, Substitute.For<ILog>()))
                {
                    letMeIn.Should().NotBeNull();
                }

                using (var letMeInAgain = OctoMonitor.Enter(this, "waiting", cts.Token, Substitute.For<ILog>()))
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
                using (var letMeIn = OctoMonitor.Enter(this, "waiting", cts.Token, Substitute.For<ILog>()))
                {
                    letMeIn.Should().NotBeNull();
                    using (var letMeInAgain = OctoMonitor.Enter(this, "waiting", cts.Token, Substitute.For<ILog>()))
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
                using (var letMeIn = OctoMonitor.Enter(first, "waiting", cts.Token, Substitute.For<ILog>()))
                {
                    using (var meToo = OctoMonitor.Enter(second, "waiting", cts.Token, Substitute.For<ILog>()))
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
                using (letMeIn = OctoMonitor.Enter(this, "waiting1", cts.Token, Substitute.For<ILog>()))
                {
                    var thread = new Thread(() =>
                    {
                        Assert.Throws<OperationCanceledException>(() => meToo = OctoMonitor.Enter(this, "waiting2", cts.Token, Substitute.For<ILog>()), "time should have expired before we entered the monitor");
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

            var log = new InMemoryLog();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var wait = new AutoResetEvent(false);
                using (OctoMonitor.Enter(this, "waiting1", cts.Token, log))
                {
                    var thread = new Thread(() =>
                    {
                        Assert.Throws<OperationCanceledException>(() => OctoMonitor.Enter(this, expectedWaitMessage, cts.Token, log), "time should have expired before we entered the monitor");
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