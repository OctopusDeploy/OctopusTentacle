using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    /// <summary>
    /// Tests for the startup gate itself. These do not start a Tentacle: they model what the builders do around it
    /// (take the gate, run a startup sequence, release it before the test body runs) so the ordering and the
    /// cleanup behaviour can be asserted without depending on a machine that can start Tentacles.
    /// </summary>
    [NonParallelizable]
    public class TentacleStartupGateFixture
    {
        static readonly TimeSpan AssertionTimeout = TimeSpan.FromSeconds(30);

        ILogger logger;

        [SetUp]
        public void SetUp()
        {
            logger = new SerilogLoggerBuilder().Build().ForContext<TentacleStartupGateFixture>();
        }

        [Test]
        public async Task OnlyOneStartupRunsAtATime_RegardlessOfWhichBuilderStartedIt()
        {
            var gate = new TentacleStartupGate(isEnabled: true);
            var events = new ConcurrentQueue<string>();

            var listeningStartupHasStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var listeningStartupCanFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pollingStartupHasStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var listening = Task.Run(async () =>
            {
                using (await gate.AcquireAsync("listening", logger, CancellationToken.None))
                {
                    events.Enqueue("listening-started");
                    listeningStartupHasStarted.SetResult(true);
                    await listeningStartupCanFinish.Task;
                    events.Enqueue("listening-finished");
                }
            });

            await WithTimeout(listeningStartupHasStarted.Task);

            var polling = Task.Run(async () =>
            {
                using (await gate.AcquireAsync("polling", logger, CancellationToken.None))
                {
                    events.Enqueue("polling-started");
                    pollingStartupHasStarted.SetResult(true);
                }
            });

            // The polling startup cannot begin while the listening one holds the gate.
            await AssertDoesNotComplete(pollingStartupHasStarted.Task);
            events.Should().NotContain("polling-started");

            listeningStartupCanFinish.SetResult(true);

            await WithTimeout(Task.WhenAll(listening, polling));

            events.Should().ContainInOrder("listening-started", "listening-finished", "polling-started");
        }

        [Test]
        public async Task AStartupWaitingForReadinessStillHoldsTheGate()
        {
            var gate = new TentacleStartupGate(isEnabled: true);

            var tentacleIsReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waitingForReadiness = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var nextStartupHasTheGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var first = Task.Run(async () =>
            {
                using (await gate.AcquireAsync("first", logger, CancellationToken.None))
                {
                    waitingForReadiness.SetResult(true);
                    await tentacleIsReady.Task;
                }
            });

            await WithTimeout(waitingForReadiness.Task);

            var second = Task.Run(async () =>
            {
                using (await gate.AcquireAsync("second", logger, CancellationToken.None))
                {
                    nextStartupHasTheGate.SetResult(true);
                }
            });

            await AssertDoesNotComplete(nextStartupHasTheGate.Task);

            tentacleIsReady.SetResult(true);

            await WithTimeout(Task.WhenAll(first, second));
            nextStartupHasTheGate.Task.Result.Should().BeTrue();
        }

        [Test]
        public async Task TestBodiesRunInParallelOnceStartupHasFinished()
        {
            var gate = new TentacleStartupGate(isEnabled: true);

            var bothTestBodiesAreRunning = new CountdownEvent(2);
            var bothAreOverlapping = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task BuildThenRunTheTestBody(string instanceName)
            {
                using (await gate.AcquireAsync(instanceName, logger, CancellationToken.None))
                {
                    // The startup sequence, which is serialized.
                }

                // The test body, which is not.
                if (bothTestBodiesAreRunning.Signal()) bothAreOverlapping.SetResult(true);
                await bothAreOverlapping.Task;
            }

            await WithTimeout(Task.WhenAll(BuildThenRunTheTestBody("first"), BuildThenRunTheTestBody("second")));

            gate.IsHeld.Should().BeFalse();
        }

        [Test]
        public async Task TheGateIsReleasedWhenAStartupFails()
        {
            var gate = new TentacleStartupGate(isEnabled: true);

            Func<Task> failingStartup = async () =>
            {
                using (await gate.AcquireAsync("fails", logger, CancellationToken.None))
                {
                    throw new Exception("Tentacle did not appear to start correctly");
                }
            };

            await failingStartup.Should().ThrowAsync<Exception>();

            gate.IsHeld.Should().BeFalse();
            await WithTimeout(TakeAndReleaseTheGate(gate, "next"));
        }

        [Test]
        public async Task TheGateIsReleasedWhenAStartupIsCancelled()
        {
            var gate = new TentacleStartupGate(isEnabled: true);
            using var cancellationTokenSource = new CancellationTokenSource();

            Func<Task> cancelledStartup = async () =>
            {
                using (await gate.AcquireAsync("cancelled", logger, cancellationTokenSource.Token))
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                }
            };

            await cancelledStartup.Should().ThrowAsync<OperationCanceledException>();

            gate.IsHeld.Should().BeFalse();
            await WithTimeout(TakeAndReleaseTheGate(gate, "next"));
        }

        [Test]
        public async Task AQueuedStartupCanBeCancelledAndDoesNotTakeTheGate()
        {
            var gate = new TentacleStartupGate(isEnabled: true);
            using var cancellationTokenSource = new CancellationTokenSource();

            var holderCanFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var holderHasTheGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var holder = Task.Run(async () =>
            {
                using (await gate.AcquireAsync("holder", logger, CancellationToken.None))
                {
                    holderHasTheGate.SetResult(true);
                    await holderCanFinish.Task;
                }
            });

            await WithTimeout(holderHasTheGate.Task);

            var queued = gate.AcquireAsync("queued", logger, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();

            Func<Task> awaitQueued = async () => await queued;
            await awaitQueued.Should().ThrowAsync<OperationCanceledException>();

            holderCanFinish.SetResult(true);
            await WithTimeout(holder);

            // The cancelled startup never took the gate, so it is free for the next one.
            gate.IsHeld.Should().BeFalse();
            await WithTimeout(TakeAndReleaseTheGate(gate, "next"));
        }

        [Test]
        public async Task CleanupAfterAFailedStartupDoesNotNeedTheGate()
        {
            // A failed StartTentacle disposes its RunningTentacle while the build still holds the gate. If deleting
            // the instance needed the gate too, that would deadlock.
            var gate = new TentacleStartupGate(isEnabled: true);
            var instanceWasDeleted = false;

            Func<Task> failingBuild = async () =>
            {
                using (await gate.AcquireAsync("fails", logger, CancellationToken.None))
                {
                    try
                    {
                        throw new Exception("Tentacle did not appear to start correctly");
                    }
                    catch
                    {
                        // Stands in for RunningTentacle.DisposeAsync, which deletes the instance.
                        gate.IsHeld.Should().BeTrue("cleanup runs while the gate is still held");
                        instanceWasDeleted = true;
                        throw;
                    }
                }
            };

            await failingBuild.Should().ThrowAsync<Exception>();

            instanceWasDeleted.Should().BeTrue();
            gate.IsHeld.Should().BeFalse();
            await WithTimeout(TakeAndReleaseTheGate(gate, "next"));
        }

        [Test]
        public async Task StartupsAreNotSerializedWhenTheGateIsDisabled()
        {
            var gate = new TentacleStartupGate(isEnabled: false);

            var bothStartupsAreRunning = new CountdownEvent(2);
            var bothAreOverlapping = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task Startup(string instanceName)
            {
                using (await gate.AcquireAsync(instanceName, logger, CancellationToken.None))
                {
                    if (bothStartupsAreRunning.Signal()) bothAreOverlapping.SetResult(true);
                    await bothAreOverlapping.Task;
                }
            }

            await WithTimeout(Task.WhenAll(Startup("first"), Startup("second")));
        }

        [Test]
        public async Task ALeaseCanBeDisposedMoreThanOnceWithoutFreeingTheGateTwice()
        {
            var gate = new TentacleStartupGate(isEnabled: true);

            Task<IDisposable> thirdLease;

            var lease = await gate.AcquireAsync("first", logger, CancellationToken.None);
            lease.Dispose();
            lease.Dispose();

            // If the double dispose had released the gate twice, two startups could run at once.
            using (await gate.AcquireAsync("second", logger, CancellationToken.None))
            {
                gate.IsHeld.Should().BeTrue();

                var third = gate.AcquireAsync("third", logger, CancellationToken.None);
                await AssertDoesNotComplete(third);

                thirdLease = third;
            }

            (await WithTimeout(thirdLease)).Dispose();
        }

        static async Task TakeAndReleaseTheGate(TentacleStartupGate gate, string instanceName)
        {
            using (await gate.AcquireAsync(instanceName, new SerilogLoggerBuilder().Build(), CancellationToken.None))
            {
            }
        }

        static async Task WithTimeout(Task task)
        {
            await task.WaitAsync(AssertionTimeout);
        }

        static async Task<T> WithTimeout<T>(Task<T> task)
        {
            return await task.WaitAsync(AssertionTimeout);
        }

        static async Task AssertDoesNotComplete(Task task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(500)));
            completed.Should().NotBe(task, "it should still be waiting for the gate");
        }

    }
}
