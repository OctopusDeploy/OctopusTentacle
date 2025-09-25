using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using Halibut.Exceptions;
using Halibut.Transport;
using NUnit.Framework;
using Octopus.Tentacle.Client.Retries;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class RpcCallRetryHandlerFixture
    {
        readonly TimeSpan retryBackoffBuffer = TimeSpan.FromSeconds(2);

        [Test]
        public async Task ReturnsTheResultWhenNoRetries()
        {
            var expectedResult = Guid.NewGuid();

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60));

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
        
        [Test]
        public async Task ReturnsTheResultAfterARetry()
        {
            var expectedResult = Guid.NewGuid();
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60));

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
        
        [Test]
        public async Task RetriesHalibutExceptions()
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60));

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

        [Test]
        public async Task DoesNotRetryHalibutExceptionsThatAreKnownToNotBeNetworkErrors()
        {
            var callCount = 0;
            var onRetryActionCalled = false;
            var onTimeoutActionCalled = false;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60));

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        callCount++;
                        throw new NoMatchingServiceOrMethodHalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (_, _, _, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        onRetryActionCalled = true;
                    },
                    onTimeoutAction: async (_, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        onTimeoutActionCalled = true;
                    },
                    CancellationToken.None);
            }
            catch (NoMatchingServiceOrMethodHalibutClientException ex) when (ex.Message == "An error has occurred.")
            {
            }

            callCount.Should().Be(1);
            onRetryActionCalled.Should().BeFalse();
            onTimeoutActionCalled.Should().BeFalse();
        }

        [Test]
        public async Task DoesNotRetryGenericExceptions()
        {
            var callCount = 0;
            var onRetryActionCalled = false;
            var onTimeoutActionCalled = false;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60));

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        callCount++;
                        throw new Exception("An error has occurred.");
                    },
                    onRetryAction: async (_, _, _, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        onRetryActionCalled = true;
                    },
                    onTimeoutAction: async (_, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        onTimeoutActionCalled = true;
                    },
                    CancellationToken.None);
            }
            catch (Exception ex) when (ex.Message == "An error has occurred.")
            {

            }

            callCount.Should().Be(1);
            onRetryActionCalled.Should().BeFalse();
            onTimeoutActionCalled.Should().BeFalse();
        }

        [Test]
        public async Task DoesNotRetryIfTheInitialRequestTakesLongerThanTheRetryDuration()
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(2));
            var calledOnRetryAction = false;
            var calledOnTimeoutAction = false;

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        callCount++;
                        await Task.Delay(TimeSpan.FromSeconds(8), CancellationToken.None);
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (_, _, _, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        calledOnRetryAction = true;
                    },
                    onTimeoutAction: async (_, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        calledOnTimeoutAction = true;
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException)
            {
            }

            callCount.Should().Be(1);
            calledOnRetryAction.Should().BeFalse();
            calledOnTimeoutAction.Should().BeTrue();
        }

        [Test]
        public async Task DoesNotRetryIfTheExecutingDurationIsLongerThanTheRetryDuration()
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(15));
            var calledOnRetryAction = false;
            var calledOnRetryActionAfterRetryDuration = false;
            var calledOnTimeoutAction = false;

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        callCount++;
                        await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (_, sleepDuration, _, _, elapsedDuration, _) =>
                    {
                        await Task.CompletedTask;
                        calledOnRetryAction = true;

                        if (elapsedDuration + sleepDuration + TimeSpan.FromSeconds(1) > handler.RetryTimeout)
                        {
                            calledOnRetryActionAfterRetryDuration = true;
                        }
                    },
                    onTimeoutAction: async (_, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        calledOnTimeoutAction = true;
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException)
            {
            }

            callCount.Should().BeGreaterThan(1);
            calledOnRetryAction.Should().BeTrue();
            calledOnRetryActionAfterRetryDuration.Should().BeFalse();
            calledOnTimeoutAction.Should().BeTrue();
        }

        [Test]
        public async Task DoesNotRetryIfNoExceptionOccurs()
        {
            var callCount = 0;
            var onRetryActionCalled = false;
            var onTimeoutActionCalled = false;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60));

            await handler.ExecuteWithRetries(
                async _ =>
                {
                    await Task.CompletedTask;
                    callCount++;

                    return Guid.NewGuid();
                },
                onRetryAction: async (_, _, _, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onRetryActionCalled = true;
                },
                onTimeoutAction: async (_, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onTimeoutActionCalled = true;
                },
                CancellationToken.None);

            callCount.Should().Be(1);
            onRetryActionCalled.Should().BeFalse();
            onTimeoutActionCalled.Should().BeFalse();
        }

        [Test]
        public async Task RetriesExceptionsForTheConfiguredTimeoutBeforeCancelling()
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10));

            var stopWatch = new Stopwatch();

            var retries = new List<(TimeSpan sleepDuration, int retryCount, TimeSpan timeout, TimeSpan elapsedDuration)>();

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
                    onRetryAction: async (_, sleepDuration, retryCount, timeout, elapsedDuration, _) =>
                    {
                        await Task.CompletedTask;

                        retries.Add((sleepDuration, retryCount, timeout, elapsedDuration));
                    },
                    onTimeoutAction: async (_, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        stopWatch.Stop();
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            var lastExpectedRetry = retries.Last(x => x.elapsedDuration + x.sleepDuration < handler.RetryTimeout);

            stopWatch.Elapsed.Should()
                .BeGreaterOrEqualTo(TimeSpan.FromSeconds(5), "Fallback assertion in case the lastExpectedRetry logic is wrong")
                .And.BeGreaterOrEqualTo(lastExpectedRetry.elapsedDuration, "Calculation of how long retries should have occurred for")
                .And.BeLessThan(TimeSpan.FromSeconds(20), "Upper limit to ensure it is not retrying for a lot longer than expected.");
            callCount.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task RetriesShouldTakeIntoAccountTheSleepDuration()
        {
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10));
            var retries = new List<(TimeSpan sleepDuration, int retryCount, TimeSpan timeout, TimeSpan elapsedDuration)>();

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (_, sleepDuration, retryCount, timeout, elapsedDuration, _) =>
                    {
                        await Task.CompletedTask;
                        retries.Add((sleepDuration, retryCount, timeout, elapsedDuration));
                    },
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            var lastExpectedRetry = retries.Last();
            (lastExpectedRetry.elapsedDuration + lastExpectedRetry.sleepDuration).Should().BeLessThan(handler.RetryTimeout);
        }

        [Test]
        public async Task CancelsTheExecutingActionIfItIsARetryAfterTheTimeout()
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10));

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
                    onTimeoutAction: async (_, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        stopWatch.Stop();
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            stopWatch.Elapsed.Should().BeGreaterOrEqualTo(GetMinTimeoutDuration(handler)).And.BeLessThan(TimeSpan.FromSeconds(20));
            callCount.Should().Be(2);
        }

        [Test]
        public async Task ThrowsTheLastExceptionWhenHasRetriedAndTimesOut()
        {
            var callCount = 0;
            Exception? actualException = null;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10));

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

        [Test]
        public async Task CanCancelRetries()
        {
            var callCount = 0;

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(60));
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
                    onTimeoutAction: async (_, _, _, _) =>
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
        
        [Test]
        public async Task CanPerformAnActionBeforeARetry()
        {
            var actionCount = 0;
            var onRetryActions = new List<int>();

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10));

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        actionCount++;
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (exception, sleepDuration, retryCount, totalRetryDuration, elapsedDuration, ct) =>
                    {
                        await Task.CompletedTask;
                        onRetryActions.Add(retryCount);
                    },
                    onTimeoutAction: null,
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            actionCount.Should().BeGreaterThan(1);
            onRetryActions.Count.Should().BeInRange(actionCount - 1, actionCount);
            onRetryActions[0].Should().Be(1);
            onRetryActions[1].Should().Be(2);
        }
        
        [Test]
        public async Task CanPerformAnActionBeforeTimeoutWhenARetryCausedTheTimeout()
        {
            var timeoutTimes = new List<(TimeSpan Timeout, TimeSpan ElapsedDuration, int RetryCount)>();
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(10));
            var stopwatch = new Stopwatch();
            var totalDurationUntilLastRetry = TimeSpan.Zero;
            var actionCount = 0;
            var onRetryActionCount = 0;

            try
            {
                stopwatch.Start();

                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        actionCount++;
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (_, _, _, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        onRetryActionCount++;
                        totalDurationUntilLastRetry = stopwatch.Elapsed;
                    },
                    onTimeoutAction: async (timeout, elapsedDuration, retryCount, _) =>
                    {
                        await Task.CompletedTask;
                        stopwatch.Stop();
                        timeoutTimes.Add((timeout, elapsedDuration, retryCount));
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            actionCount.Should().BeGreaterThan(1);
            timeoutTimes.Should().HaveCount(1);
            timeoutTimes[0].Timeout.TotalSeconds.Should().Be(10);
            timeoutTimes[0].ElapsedDuration.Should().BeGreaterThan(totalDurationUntilLastRetry).And.BeLessThanOrEqualTo(stopwatch.Elapsed);
            timeoutTimes[0].RetryCount.Should().BeInRange(actionCount - 1, actionCount);
            timeoutTimes[0].RetryCount.Should().Be(onRetryActionCount);
        }

        [Test]
        public async Task CanPerformAnActionBeforeTimeoutWhenTheInitialRequestCausedTheTimeout()
        {
            var timeoutTimes = new List<(TimeSpan Timeout, TimeSpan ElapsedDuration, int RetryCount)>();
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(4));
            var stopwatch = new Stopwatch();
            var totalDurationUntilLastRetry = TimeSpan.Zero;
            var actionCount = 0;
            var onRetryActionCount = 0;

            try
            {
                await handler.ExecuteWithRetries<Guid>(
                    async ct =>
                    {
                        await Task.CompletedTask;
                        actionCount++;
                        stopwatch.Start();
                        await Task.Delay(TimeSpan.FromSeconds(8), CancellationToken.None);
                        throw new HalibutClientException("An error has occurred.");
                    },
                    onRetryAction: async (_, _, _, _, _, _) =>
                    {
                        await Task.CompletedTask;
                        onRetryActionCount++;
                        totalDurationUntilLastRetry = stopwatch.Elapsed;
                    },
                    onTimeoutAction: async (timeout, elapsedDuration, retryCount, _) =>
                    {
                        await Task.CompletedTask;
                        timeoutTimes.Add((timeout, elapsedDuration, retryCount));
                    },
                    CancellationToken.None);
            }
            catch (HalibutClientException) { }

            actionCount.Should().Be(1);
            timeoutTimes.Should().HaveCount(1);
            timeoutTimes[0].Timeout.TotalSeconds.Should().Be(4);
            timeoutTimes[0].ElapsedDuration.Should().BeGreaterThan(totalDurationUntilLastRetry);
            timeoutTimes[0].RetryCount.Should().Be(0);
            onRetryActionCount.Should().Be(0);
        }
        
        [Test]
        public async Task ShouldWaitBetweenRetries()
        {
            var sleepDurations = new List<TimeSpan>();

            // This test is slow by design. It aims to ensure the back off durations are correct.
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(80));
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
        
        [Test]
        public async Task ShouldNotTimeoutTheInitialRequest()
        {
            var expectedResult = Guid.NewGuid();

            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(5));

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
        private TimeSpan GetMinTimeoutDuration(RpcCallRetryHandler handler)
        {
            return handler.RetryTimeout - handler.RetryIfRemainingDurationAtLeast - retryBackoffBuffer;
        }

        [Test]
        public async Task MinimumAttempts_WhenSetTo1_DoesNotRetryAfterTimeoutExceeded()
        {
            var expectedResult = Guid.NewGuid();
            var callCount = 0;
            var onRetryActionCalled = false;
            var onTimeoutActionCalled = false;

            // Short timeout to ensure it's exceeded, but minimumAttemptsForInterruptedLongRunningCalls = 1
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(1), minimumAttemptsForInterruptedLongRunningCalls: 1);

            var result = await handler.ExecuteWithRetries(
                async ct =>
                {
                    callCount++;
                    
                    // Sleep for 3 seconds to simulate a long-running operation
                    // must exceed retry duration
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                    
                    // Succeed on the first (and only) attempt since minimumAttemptsForInterruptedLongRunningCalls = 1
                    if (callCount == 1)
                    {
                        return expectedResult;
                    }
                    
                    throw new HalibutClientException($"An error has occurred on attempt {callCount}");
                },
                onRetryAction: async (_, _, _, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onRetryActionCalled = true;
                },
                onTimeoutAction: async (_, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onTimeoutActionCalled = true;
                },
                CancellationToken.None);

            // With minimumAttemptsForInterruptedLongRunningCalls = 1, we should only make one attempt (no retries)
            result.Should().Be(expectedResult);
            callCount.Should().Be(1);
            onRetryActionCalled.Should().BeFalse();
            onTimeoutActionCalled.Should().BeFalse();
        }

        [Test]
        public async Task MinimumAttempts_WhenSetTo2_MakesOneRetryEvenAfterTimeoutExceeded()
        {
            var expectedResult = Guid.NewGuid();
            var callCount = 0;
            var onRetryActionCalled = false;
            var onTimeoutActionCalled = false;

            // Short timeout to ensure it's exceeded, but minimumAttemptsForInterruptedLongRunningCalls = 2
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(5), minimumAttemptsForInterruptedLongRunningCalls: 2);

            var result = await handler.ExecuteWithRetries(
                async ct =>
                {
                    callCount++;
                    
                    // Delay 2 seconds to ensure the ct doesn't get canceled.
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    
                    // Fail on first attempt, succeed on second
                    if (callCount == 1)
                    {
                        throw new HalibutClientException($"An error has occurred on attempt {callCount}");
                    }
                    
                    return expectedResult;
                },
                onRetryAction: async (_, _, _, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onRetryActionCalled = true;
                },
                onTimeoutAction: async (_, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onTimeoutActionCalled = true;
                },
                CancellationToken.None);

            // With minimumAttemptsForInterruptedLongRunningCalls = 2, we should make 2 attempts (1 initial + 1 retry)
            result.Should().Be(expectedResult);
            callCount.Should().Be(2);
            onRetryActionCalled.Should().BeTrue();
            onTimeoutActionCalled.Should().BeFalse(); // Should succeed before timeout
        }

        [Test]
        public async Task MinimumAttempts_WhenSetTo3_MakesTwoRetriesEvenAfterTimeoutExceeded()
        {
            var expectedResult = Guid.NewGuid();
            var callCount = 0;
            var retryCount = 0;
            var onTimeoutActionCalled = false;

            // Short timeout to ensure it's exceeded, but minimumAttemptsForInterruptedLongRunningCalls = 3
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(8), minimumAttemptsForInterruptedLongRunningCalls: 3);

            var result = await handler.ExecuteWithRetries(
                async ct =>
                {
                    callCount++;
                    
                    // Always add 2 second delay as requested
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    
                    // Fail on first two attempts, succeed on third
                    if (callCount <= 2)
                    {
                        throw new HalibutClientException($"An error has occurred on attempt {callCount}");
                    }
                    
                    return expectedResult;
                },
                onRetryAction: async (_, _, _, _, _, _) =>
                {
                    await Task.CompletedTask;
                    retryCount++;
                },
                onTimeoutAction: async (_, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onTimeoutActionCalled = true;
                },
                CancellationToken.None);

            // With minimumAttemptsForInterruptedLongRunningCalls = 3, we should make 3 attempts (1 initial + 2 retries)
            result.Should().Be(expectedResult);
            callCount.Should().Be(3);
            retryCount.Should().Be(2);
            onTimeoutActionCalled.Should().BeFalse(); // Should succeed before timeout
        }
        
        /// <summary>
        /// Connection error means the tentacle did not get the request, which means the tentacle is considered to be offline.
        /// This shows that we won't exceed the retry duration attempting to connect to an offline tentacle to meet the
        /// minimumAttemptsForInterruptedLongRunningCalls count.
        /// </summary>
        [Test]
        public async Task WhenConfiguredToMakeAMinimumNumberOfAttempts_AndTheFirstAttemptExceedsTheRetryDuration_AndTheFailureIsAConnectingFailure_ARetryIsNotMade()
        {
            var callCount = 0;
            var onRetryActionCalled = false;
            var onTimeoutActionCalled = false;

            // Short timeout to ensure it's exceeded, but minimumAttemptsForInterruptedLongRunningCalls = 2
            var handler = new RpcCallRetryHandler(TimeSpan.FromSeconds(5), minimumAttemptsForInterruptedLongRunningCalls: 9999);

            try
            {
                await handler.ExecuteWithRetries(
                async ct =>
                {
                    callCount++;
                    
                    // Delay 2 second to ensure the ct doesn't get canceled.
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    
                    if(callCount == -1) return Guid.NewGuid(); // Never called used to make the typing work.

                    throw new HalibutClientException("", new Exception(), ConnectionState.Connecting);
                },
                onRetryAction: async (_, _, _, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onRetryActionCalled = true;
                },
                onTimeoutAction: async (_, _, _, _) =>
                {
                    await Task.CompletedTask;
                    onTimeoutActionCalled = true;
                },
                CancellationToken.None);
            }
            catch (HalibutClientException)
            {
                // Expected to throw since we're always throwing connecting exceptions
            }
            
            // We should expect the timeout limit to kick in and prevent us from making too many attempts
            callCount.Should().BeGreaterThan(1).And.BeLessThanOrEqualTo(10);
            onRetryActionCalled.Should().BeTrue();
            onTimeoutActionCalled.Should().BeTrue();
        }
    }
}
