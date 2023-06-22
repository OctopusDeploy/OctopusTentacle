using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Tests.Client
{
    [TestFixture]
    public class RpcCallRetryHandlerObservabilityDecoratorFixture
    {
        private const string RpcCallName = "GetStatus";
        private static readonly TimeSpan RetryDuration = TimeSpan.FromMinutes(1);

        [Test]
        public async Task MetricsShouldBeSuccessful_WhenNoRetries()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();

            // Act
            await ExecuteWithRetries(rpcCallObserver, DelayFor100MillisecondsAction, RetryDuration, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeSuccessful(metric);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldBeSuccessful(attempt);
        }

        [Test]
        public async Task MetricsShouldBeSuccessful_WithFailedAttempt_AfterARetry()
        {
            // Arrange
            var callCount = 0;
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new HalibutClientException("An error has occurred.");

            // Act
            await ExecuteWithRetries(rpcCallObserver, async ct =>
                {
                    var result = await DelayFor100MillisecondsAction(ct);
                    if (callCount++ == 0) throw exception;

                    return result;
                },
                RetryDuration,
                CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeSuccessful(metric);

            metric.Attempts.Should().HaveCount(2);
            ThenAttemptShouldHaveFailed(metric.Attempts[0], exception);
            ThenAttemptShouldBeSuccessful(metric.Attempts[1]);
        }

        [Test]
        public async Task MetricsShouldBeFailed_WithGenericExceptions()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new Exception("An error has occurred.");

            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, async ct =>
                        {
                            await DelayFor100MillisecondsAction(ct);
                            throw exception;
                        },
                        RetryDuration,
                        CancellationToken.None))
                .ThrowAsync<Exception>();

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeFailed(metric, RetryDuration, exception);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldHaveFailed(attempt, exception);
        }

        [Test]
        public async Task MetricsShouldBeFailed_WhenRetryTimeoutIsReached()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new HalibutClientException("An error has occurred.");
            //Timeout for long enough that we get a few attempts.
            var retryDuration = TimeSpan.FromSeconds(3);

            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, async ct =>
                        {
                            await DelayFor100MillisecondsAction(ct);
                            throw exception;
                        },
                        retryDuration,
                        CancellationToken.None))
                .ThrowAsync<HalibutClientException>();

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeFailed(metric, retryDuration, exception);

            metric.Attempts.Should().HaveCountGreaterThan(1);
            ThenAttemptShouldHaveFailed(metric.Attempts.First(), exception);
            ThenAttemptShouldHaveFailed(metric.Attempts.Last(), exception);
        }

        [Test]
        public async Task MetricsShouldBeFailed_WhenCancellationTokenIsCancelled()
        {
            // Arrange
            var callCount = 0;
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new HalibutClientException("An error has occurred.");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            
            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, async ct =>
                        {
                            await DelayFor100MillisecondsAction(ct);
                            if (callCount++ > 0)
                            {
                                cancellationTokenSource.Cancel();
                            }

                            throw exception;
                        },
                        RetryDuration,
                        cancellationToken))
                .ThrowAsync<TaskCanceledException>();

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            metric.Succeeded.Should().BeFalse();
            metric.Exception?.GetType().Should().Be(typeof(TaskCanceledException));
            metric.HasException.Should().BeTrue();
            metric.WasCancelled.Should().BeTrue();
            
            metric.Attempts.Should().HaveCount(2);
            ThenAttemptShouldHaveFailed(metric.Attempts.First(), exception);

            var attempt = metric.Attempts.Last();
            attempt.Succeeded.Should().BeFalse();
            attempt.Exception.Should().BeEquivalentTo(exception);
            attempt.WasCancelled.Should().BeTrue();
        }
        
        private static async Task ExecuteWithRetries(
            IRpcCallObserver rpcCallObserver, 
            Func<CancellationToken, Task<Guid>> action, 
            TimeSpan retryDuration, 
            CancellationToken cancellationToken)
        {
            var sut = RpcCallExecutorFactory.Create(retryDuration, rpcCallObserver);

            await sut.ExecuteWithRetries(
                RpcCallName,
                action,
                onRetryAction: null,
                onTimeoutAction: null,
                false,
                TimeSpan.FromMinutes(2),
                cancellationToken);
        }

        private static async Task<Guid> DelayFor100MillisecondsAction(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            return Guid.NewGuid();
        }
        
        private static void ThenMetricsShouldBeSuccessful(RpcCallMetrics metric)
        {
            metric.Succeeded.Should().BeTrue();
            metric.Exception.Should().BeNull();
            metric.HasException.Should().BeFalse();
            metric.WasCancelled.Should().BeFalse();

            metric.AttemptsSucceeded.Should().BeTrue();

            metric.End.Should().BeAfter(metric.Start);
            metric.Duration.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(100));

            metric.RetryTimeout.Should().Be(RetryDuration);
            metric.RpcCallName.Should().Be(RpcCallName);
        }

        private static void ThenMetricsShouldBeFailed(RpcCallMetrics metric, TimeSpan expectedRetryDuration, Exception expectedException)
        {
            metric.Succeeded.Should().BeFalse();
            metric.Exception.Should().BeEquivalentTo(expectedException);
            metric.HasException.Should().BeTrue();
            metric.WasCancelled.Should().BeFalse();

            metric.AttemptsSucceeded.Should().BeFalse();

            metric.End.Should().BeAfter(metric.Start);
            metric.Duration.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(100));

            metric.RetryTimeout.Should().Be(expectedRetryDuration);
            metric.RpcCallName.Should().Be(RpcCallName);
        }

        private static void ThenAttemptShouldBeSuccessful(TimedOperation attempt)
        {
            attempt.Succeeded.Should().BeTrue();
            attempt.Exception.Should().BeNull();
            attempt.WasCancelled.Should().BeFalse();

            attempt.End.Should().BeAfter(attempt.Start);
            attempt.Duration.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(100));
        }

        private static void ThenAttemptShouldHaveFailed(TimedOperation attempt, Exception expectedException)
        {
            attempt.Succeeded.Should().BeFalse();
            attempt.Exception.Should().BeEquivalentTo(expectedException);
            attempt.WasCancelled.Should().BeFalse();

            attempt.End.Should().BeAfter(attempt.Start);
            attempt.Duration.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(100));
        }
    }
}