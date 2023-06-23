using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Tests.Client
{
    [TestFixture]
    public class RpcCallExecutorFixture
    {
        private const string RpcCallName = "GetStatus";
        private static readonly TimeSpan RetryDuration = TimeSpan.FromMinutes(1);

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeSuccessful_WhenNoRetries()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();

            // Act
            await ExecuteWithRetries(rpcCallObserver, DelayFor100MillisecondsAction, RetryDuration, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeSuccessful(metric, RetryDuration, true);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldBeSuccessful(attempt);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeSuccessful_WithFailedAttempt_AfterARetry()
        {
            // Arrange
            var callCount = 0;
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new HalibutClientException("An error has occurred.");

            // Act
            await ExecuteWithRetries(rpcCallObserver, ct =>
                {
                    var result = DelayFor100MillisecondsAction(ct);
                    if (callCount++ == 0) throw exception;

                    return result;
                },
                RetryDuration,
                CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeSuccessful(metric, RetryDuration, true);

            metric.Attempts.Should().HaveCount(2);
            ThenAttemptShouldHaveFailed(metric.Attempts[0], exception);
            ThenAttemptShouldBeSuccessful(metric.Attempts[1]);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeFailed_WithGenericExceptions()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new Exception("An error has occurred.");

            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, ct =>
                        {
                            DelayFor100MillisecondsAction(ct);
                            throw exception;
                        },
                        RetryDuration,
                        CancellationToken.None))
                .ThrowAsync<Exception>();

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeFailed(metric, RetryDuration, exception, true);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldHaveFailed(attempt, exception);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeFailed_WhenRetryTimeoutIsReached()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new HalibutClientException("An error has occurred.");
            //Timeout for long enough that we get a few attempts.
            var retryDuration = TimeSpan.FromSeconds(3);

            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, ct =>
                        {
                            DelayFor100MillisecondsAction(ct);
                            throw exception;
                        },
                        retryDuration,
                        CancellationToken.None))
                .ThrowAsync<HalibutClientException>();

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeFailed(metric, retryDuration, exception, true);

            metric.Attempts.Should().HaveCountGreaterThan(1);
            ThenAttemptShouldHaveFailed(metric.Attempts.First(), exception);
            ThenAttemptShouldHaveFailed(metric.Attempts.Last(), exception);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeFailed_WhenCancellationTokenIsCancelled()
        {
            // Arrange
            var callCount = 0;
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new HalibutClientException("An error has occurred.");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            
            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, ct =>
                        {
                            DelayFor100MillisecondsAction(ct);
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

        [Test]
        public void Execute_WithResult_MetricsShouldBeSuccessful_WhenNoRetries()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();

            // Act
            ExecuteResult(rpcCallObserver, DelayFor100MillisecondsAction, RetryDuration, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeSuccessful(metric, TimeSpan.Zero, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldBeSuccessful(attempt);
        }

        [Test]
        public void Execute_WithResult_MetricsShouldBeFailed_WithExceptions()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new Exception("An error has occurred.");

            // Act
            AssertionExtensions.Should(() => ExecuteResult(rpcCallObserver, ct =>
                        {
                            DelayFor100MillisecondsAction(ct);
                            throw exception;
                        },
                        RetryDuration,
                        CancellationToken.None)).Throw<Exception>();

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeFailed(metric, TimeSpan.Zero, exception, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldHaveFailed(attempt, exception);
        }

        [Test]
        public void Execute_Void_MetricsShouldBeSuccessful_WhenNoRetries()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();

            // Act
            ExecuteVoid(rpcCallObserver, ct => DelayFor100MillisecondsAction(ct), RetryDuration, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeSuccessful(metric, TimeSpan.Zero, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldBeSuccessful(attempt);
        }

        [Test]
        public void Execute_Void_MetricsShouldBeFailed_WithExceptions()
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            var exception = new Exception("An error has occurred.");

            // Act
            AssertionExtensions.Should(() => ExecuteVoid(rpcCallObserver, ct =>
                {
                    DelayFor100MillisecondsAction(ct);
                    throw exception;
                },
                RetryDuration,
                CancellationToken.None)).Throw<Exception>();

            // Assert
            var metric = rpcCallObserver.Metrics.Should().ContainSingle().Subject;

            ThenMetricsShouldBeFailed(metric, TimeSpan.Zero, exception, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldHaveFailed(attempt, exception);
        }

        private static async Task ExecuteWithRetries(
            IRpcCallObserver rpcCallObserver, 
            Func<CancellationToken, Guid> action, 
            TimeSpan retryDuration, 
            CancellationToken cancellationToken)
        {
            var sut = RpcCallExecutorFactory.Create(retryDuration, rpcCallObserver);

            await sut.ExecuteWithRetries(
                RpcCallName,
                action,
                Substitute.For<ILog>(),
                false,
                cancellationToken);
        }

        private static Guid ExecuteResult(
            IRpcCallObserver rpcCallObserver,
            Func<CancellationToken, Guid> action,
            TimeSpan retryDuration,
            CancellationToken cancellationToken)
        {
            var sut = RpcCallExecutorFactory.Create(retryDuration, rpcCallObserver);

            return sut.Execute(
                RpcCallName,
                action,
                cancellationToken);
        }

        private static void ExecuteVoid(
            IRpcCallObserver rpcCallObserver,
            Action<CancellationToken> action,
            TimeSpan retryDuration,
            CancellationToken cancellationToken)
        {
            var sut = RpcCallExecutorFactory.Create(retryDuration, rpcCallObserver);

            sut.Execute(
                RpcCallName,
                action,
                cancellationToken);
        }

        private static Guid DelayFor100MillisecondsAction(CancellationToken cancellationToken)
        {
            Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).Wait(cancellationToken);

            return Guid.NewGuid();
        }
        
        private static void ThenMetricsShouldBeSuccessful(RpcCallMetrics metric, TimeSpan expectedRetryDuration, bool expectedWithRetries)
        {
            metric.Succeeded.Should().BeTrue();
            metric.Exception.Should().BeNull();
            metric.HasException.Should().BeFalse();
            metric.WasCancelled.Should().BeFalse();

            metric.AttemptsSucceeded.Should().BeTrue();

            metric.End.Should().BeAfter(metric.Start);
            metric.Duration.Should().Be(metric.End - metric.Start);

            metric.RetryTimeout.Should().Be(expectedRetryDuration);
            metric.RpcCallName.Should().Be(RpcCallName);
            metric.WithRetries.Should().Be(expectedWithRetries);
        }

        private static void ThenMetricsShouldBeFailed(RpcCallMetrics metric, TimeSpan expectedRetryDuration, Exception expectedException, bool expectedWithRetries)
        {
            metric.Succeeded.Should().BeFalse();
            metric.Exception.Should().BeEquivalentTo(expectedException);
            metric.HasException.Should().BeTrue();
            metric.WasCancelled.Should().BeFalse();

            metric.AttemptsSucceeded.Should().BeFalse();

            metric.End.Should().BeAfter(metric.Start);
            metric.Duration.Should().Be(metric.End - metric.Start);

            metric.RetryTimeout.Should().Be(expectedRetryDuration);
            metric.RpcCallName.Should().Be(RpcCallName);
            metric.WithRetries.Should().Be(expectedWithRetries);
        }

        private static void ThenAttemptShouldBeSuccessful(TimedOperation attempt)
        {
            attempt.Succeeded.Should().BeTrue();
            attempt.Exception.Should().BeNull();
            attempt.WasCancelled.Should().BeFalse();

            attempt.End.Should().BeAfter(attempt.Start);
            attempt.Duration.Should().Be(attempt.End - attempt.Start);
        }

        private static void ThenAttemptShouldHaveFailed(TimedOperation attempt, Exception expectedException)
        {
            attempt.Succeeded.Should().BeFalse();
            attempt.Exception.Should().BeEquivalentTo(expectedException);
            attempt.WasCancelled.Should().BeFalse();

            attempt.End.Should().BeAfter(attempt.Start);
            attempt.Duration.Should().Be(attempt.End - attempt.Start);
        }
    }
}