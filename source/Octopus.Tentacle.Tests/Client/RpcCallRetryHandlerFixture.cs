using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using Halibut.Exceptions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Retries;
using Polly.Timeout;

namespace Octopus.Tentacle.Tests.Client
{
    [TestFixture]
    public class RpcCallRetryHandlerFixture
    {
        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task ReturnsTheResultWhenNoRetries(TimeoutStrategy timeoutStrategy)
        {
            var expectedResult = Guid.NewGuid();

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);

            var result = await handler.ExecuteWithRetries(
                async ct =>
                {
                    await Task.CompletedTask;

                    return expectedResult;
                },
                onRetryAction: null,
                onTimeoutAction: null,
                CancellationToken.None);

            result.Should().Be(expectedResult);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task ReturnsTheResultAfterARetry(TimeoutStrategy timeoutStrategy)
        {
            var expectedResult = Guid.NewGuid();
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);

            var result = await handler.ExecuteWithRetries(
                async ct =>
                {
                    await Task.CompletedTask;
                    callCount++;

                    if (callCount > 1)
                    {
                        return expectedResult;
                    }

                    throw new HalibutClientException("An error has occurred.");
                },
                onRetryAction: null,
                onTimeoutAction: null,
                CancellationToken.None);

            callCount.Should().BeGreaterThan(1);
            result.Should().Be(expectedResult);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task RetriesHalibutExceptions(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);

            try
            {
                await handler.ExecuteWithRetries(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        callCount++;

                        if (callCount > 1)
                        {
                            return Guid.NewGuid();
                        }

                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: null,
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            callCount.Should().BeGreaterThan(1);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task DoesNotRetryHalibutExceptionsThatAreKnownToNotBeNetworkErrors(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        callCount++;
                        throw new NoMatchingServiceOrMethodHalibutClientException("An error has occurred.");
                    },
                    onRetryAction: null,
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (NoMatchingServiceOrMethodHalibutClientException ex) when (ex.Message == "An error has occurred.")
            {
            }

            callCount.Should().Be(1);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task DoesNotRetryGenericExceptions(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        callCount++;
                        throw new Exception("An error has occurred.");
                    },
                    onRetryAction: null,
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (Exception ex) when (ex.Message == "An error has occurred.")
            {

            }

            callCount.Should().Be(1);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task DoesNotRetryIfNoExceptionOccurs(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);

            await handler.ExecuteWithRetries(
                async _ =>
                {
                    await Task.CompletedTask;
                    callCount++;

                    return Guid.NewGuid();
                },
                onRetryAction: null,
                onTimeoutAction: null,
                CancellationToken.None);

            callCount.Should().Be(1);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task RetriesExceptionsForTheConfiguredTimeoutBeforeCancelling(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10), timeoutStrategy);

            var stopWatch = new Stopwatch();

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        if (!stopWatch.IsRunning)
                        {
                            stopWatch.Start();
                        }
                        await Task.CompletedTask;
                        callCount++;
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: null,
                    onTimeoutAction: async (_, _) =>
                    {
                        await Task.CompletedTask;
                        stopWatch.Stop();
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            stopWatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(8)).And.BeLessThan(TimeSpan.FromSeconds(20));
            callCount.Should().BeGreaterThan(1);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task CancelsTheExecutingActionIfItIsARetryAfterTheTimeout(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10), timeoutStrategy);

            var stopWatch = new Stopwatch();

            try
            {
                await handler.ExecuteWithRetries(
                    async ct =>
                    {
                        if (!stopWatch.IsRunning)
                        {
                            stopWatch.Start();
                        }
                        callCount++;

                        if (callCount == 1)
                        {
                            throw new HalibutClientException("An error has occurred.");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(60), ct);

                        return Guid.NewGuid();
                    },
                    onRetryAction: null,
                    onTimeoutAction: async (_, _) =>
                    {
                        await Task.CompletedTask;
                        stopWatch.Stop();
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            stopWatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(8)).And.BeLessThan(TimeSpan.FromSeconds(20));
            callCount.Should().Be(2);
        }


        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task CancelsTheExecutingActionIfItIsARetryAfterTheTimeoutEvenIfActionIgnoresCancellation(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10), timeoutStrategy);

            var stopWatch = new Stopwatch();

            try
            {
                await handler.ExecuteWithRetries(
                    async ct =>
                    {
                        if (!stopWatch.IsRunning)
                        {
                            stopWatch.Start();
                        }

                        var wontCancelCancellationToken = new CancellationTokenSource().Token;

                        callCount++;
                        if (callCount == 1)
                        {
                            throw new HalibutClientException("An error has occurred.");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(60), wontCancelCancellationToken);

                        return Guid.NewGuid();
                    },
                    onRetryAction: null,
                    onTimeoutAction: async (_, _) =>
                    {
                        await Task.CompletedTask;
                        stopWatch.Stop();
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            stopWatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(8)).And.BeLessThan(TimeSpan.FromSeconds(20));
            callCount.Should().Be(2);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task ThrowsTheLastExceptionWhenHasRetriedAndTimesOut(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;
            Exception? actualException = null;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10), timeoutStrategy);

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        callCount++;
                        throw new HalibutClientException(callCount.ToString());
                    },
                    onRetryAction: null,
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            callCount.Should().BeGreaterThan(1);
            actualException.Should().NotBeNull();
            actualException.Should().BeOfType<HalibutClientException>();
            actualException!.Message.Should().Be(callCount.ToString());
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task CanCancelRetries(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);
            var cancellationToken = GetCancellationToken(10);

            var stopWatch = new Stopwatch();

            try
            {
                await handler.ExecuteWithRetries(
                    async ct =>
                    {
                        if (!stopWatch.IsRunning)
                        {
                            stopWatch.Start();
                        }
                        callCount++;
                        await Task.Delay(TimeSpan.FromSeconds(60), ct);

                        return Guid.NewGuid();
                    },
                    onRetryAction: null,
                    onTimeoutAction: async (_, _) =>
                    {
                        await Task.CompletedTask;
                        stopWatch.Stop();
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException) { }

            stopWatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(8)).And.BeLessThan(TimeSpan.FromSeconds(20));
            callCount.Should().Be(1);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task CanPerformAnActionBeforeARetry(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;
            var retryCounts = new List<int>();

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10), timeoutStrategy);

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        callCount++;
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (exception, sleepDuration, retryCount, totalRetryDuration, elapsedDuration, ct) =>
                    {
                        await Task.CompletedTask;
                        retryCounts.Add(retryCount);
                    },
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            callCount.Should().BeGreaterThan(1);
            retryCounts.Should().HaveCount(callCount);
            retryCounts[0].Should().Be(1);
            retryCounts[1].Should().Be(2);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task CanPerformAnActionBeforeATimeout(TimeoutStrategy timeoutStrategy)
        {
            var timeoutTimes = new List<TimeSpan>();

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10), timeoutStrategy);

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: null,
                    onTimeoutAction: async (timeout, ct) =>
                    {
                        await Task.CompletedTask;
                        timeoutTimes.Add(timeout);
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            timeoutTimes.Should().HaveCount(1);
            timeoutTimes[0].TotalSeconds.Should().Be(10);
        }


        [TestCase(TimeoutStrategy.Optimistic)]
        public async Task ShouldWaitBetweenRetries(TimeoutStrategy timeoutStrategy)
        {
            var sleepDurations = new List<TimeSpan>();

            // This test is slow by design. It aims to ensure the back off durations are correct.
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(80), timeoutStrategy);
            var started = Stopwatch.StartNew();

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (exception, sleepDuration, retryCount, totalRetryDuration, elapsedDuration, ct) =>
                    {
                        await Task.CompletedTask;
                        sleepDurations.Add(sleepDuration);
                    },
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            started.Stop();

            var expectedDurationAbout = 0;

            for (var i = 0; i < sleepDurations.Count; i++)
            {
                var sleepDuration = sleepDurations[i];
                var expectedDuration = Math.Min(i + 1, 10);

                sleepDuration.TotalSeconds.Should().Be(expectedDuration);

                expectedDurationAbout += (int)sleepDuration.TotalSeconds;
            }

            started.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(expectedDurationAbout), TimeSpan.FromSeconds(10));
        }

        [Ignore("SAST: Currently not supported. If we need support we can revisit")]
        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task CanCancelRetriesEvenIfActionIgnoresCancellation(TimeoutStrategy timeoutStrategy)
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60), timeoutStrategy);
            var cancellationToken = GetCancellationToken(5);

            var stopWatch = new Stopwatch();

            try
            {
                await handler.ExecuteWithRetries(
                    async ct =>
                    {
                        if (!stopWatch.IsRunning)
                        {
                            stopWatch.Start();
                        }
                        var wontCancelCancellationToken = new CancellationTokenSource().Token;

                        callCount++;
                        await Task.Delay(TimeSpan.FromSeconds(60), wontCancelCancellationToken);

                        return Guid.NewGuid();
                    },
                    onRetryAction: null,
                    onTimeoutAction: async (_, _) =>
                    {
                        await Task.CompletedTask;
                        stopWatch.Stop();
                    },
                    CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            catch (TimeoutRejectedException) { }

            stopWatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(4)).And.BeLessThan(TimeSpan.FromSeconds(10));
            callCount.Should().Be(1);
        }

        [TestCase(TimeoutStrategy.Optimistic)]
        [TestCase(TimeoutStrategy.Pessimistic)]
        public async Task ShouldNotTimeoutTheInitialRequest(TimeoutStrategy timeoutStrategy)
        {
            var expectedResult = Guid.NewGuid();

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(5), timeoutStrategy);

            var result = await handler.ExecuteWithRetries(
                async ct =>
                {
                    await Task.CompletedTask;

                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                    ct.ThrowIfCancellationRequested();

                    return expectedResult;
                },
                onRetryAction: null,
                onTimeoutAction: null,
                CancellationToken.None);

            result.Should().Be(expectedResult);
        }

        static CancellationToken GetCancellationToken(int timeoutInSeconds)
        {
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds));
            var cancellationToken = cancellationTokenSource.Token;
            return cancellationToken;
        }
    }
}
