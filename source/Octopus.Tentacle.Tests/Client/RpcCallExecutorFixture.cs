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
using Octopus.Tentacle.Client.Observability;
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
            var rpcCallObserver = new TestTentacleClientObserver();
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            await ExecuteWithRetries(rpcCallObserver, DelayFor100MillisecondsAction, RetryDuration, clientOperationMetricsBuilder, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeSuccessful(metric, RetryDuration, true);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldBeSuccessful(attempt);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeSuccessful_WithFailedAttempt_AfterARetry()
        {
            // Arrange
            var callCount = 0;
            var rpcCallObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("An error has occurred.");
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            await ExecuteWithRetries(rpcCallObserver, ct =>
                {
                    var result = DelayFor100MillisecondsAction(ct);
                    if (callCount++ == 0) throw exception;

                    return result;
                },
                RetryDuration, clientOperationMetricsBuilder, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeSuccessful(metric, RetryDuration, true);

            metric.Attempts.Should().HaveCount(2);
            ThenAttemptShouldHaveFailed(metric.Attempts[0], exception);
            ThenAttemptShouldBeSuccessful(metric.Attempts[1]);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeFailed_WithGenericExceptions()
        {
            // Arrange
            var rpcCallObserver = new TestTentacleClientObserver();
            var exception = new Exception("An error has occurred.");
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, ct =>
                        {
                            DelayFor100MillisecondsAction(ct);
                            throw exception;
                        },
                        RetryDuration, clientOperationMetricsBuilder, CancellationToken.None))
                .ThrowAsync<Exception>();

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeFailed(metric, RetryDuration, exception, true);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldHaveFailed(attempt, exception);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeFailed_WhenRetryTimeoutIsReached()
        {
            // Arrange
            var rpcCallObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("An error has occurred.");
            //Timeout for long enough that we get a few attempts.
            var retryDuration = TimeSpan.FromSeconds(3);
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            await AssertionExtensions.Should(
                    () => ExecuteWithRetries(rpcCallObserver, ct =>
                        {
                            DelayFor100MillisecondsAction(ct);
                            throw exception;
                        },
                        retryDuration, clientOperationMetricsBuilder, CancellationToken.None))
                .ThrowAsync<HalibutClientException>();

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeFailed(metric, retryDuration, exception, true);

            metric.Attempts.Should().HaveCountGreaterThan(1);
            ThenAttemptShouldHaveFailed(metric.Attempts.First(), exception);
            ThenAttemptShouldHaveFailed(metric.Attempts.Last(), exception);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public async Task ExecuteWithRetries_MetricsShouldBeFailed_WhenCancellationTokenIsCancelled()
        {
            // Arrange
            var callCount = 0;
            var rpcCallObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("An error has occurred.");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

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
                        RetryDuration, clientOperationMetricsBuilder, cancellationToken))
                .ThrowAsync<TaskCanceledException>();

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

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

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public void Execute_WithResult_MetricsShouldBeSuccessful_WhenNoRetries()
        {
            // Arrange
            var rpcCallObserver = new TestTentacleClientObserver();
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            ExecuteResult(rpcCallObserver, DelayFor100MillisecondsAction, RetryDuration, clientOperationMetricsBuilder, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeSuccessful(metric, TimeSpan.Zero, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldBeSuccessful(attempt);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public void Execute_WithResult_MetricsShouldBeFailed_WithExceptions()
        {
            // Arrange
            var rpcCallObserver = new TestTentacleClientObserver();
            var exception = new Exception("An error has occurred.");
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            AssertionExtensions.Should(() => ExecuteResult(rpcCallObserver, ct =>
                {
                    DelayFor100MillisecondsAction(ct);
                    throw exception;
                },
                        RetryDuration, clientOperationMetricsBuilder, CancellationToken.None)).Throw<Exception>();

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeFailed(metric, TimeSpan.Zero, exception, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldHaveFailed(attempt, exception);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public void Execute_Void_MetricsShouldBeSuccessful_WhenNoRetries()
        {
            // Arrange
            var rpcCallObserver = new TestTentacleClientObserver();
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            ExecuteVoid(rpcCallObserver, ct => DelayFor100MillisecondsAction(ct), RetryDuration, clientOperationMetricsBuilder, CancellationToken.None);

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeSuccessful(metric, TimeSpan.Zero, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldBeSuccessful(attempt);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        [Test]
        public void Execute_Void_MetricsShouldBeFailed_WithExceptions()
        {
            // Arrange
            var rpcCallObserver = new TestTentacleClientObserver();
            var exception = new Exception("An error has occurred.");
            var clientOperationMetricsBuilder = new ClientOperationMetricsBuilder(DateTimeOffset.UtcNow);

            // Act
            AssertionExtensions.Should(() => ExecuteVoid(rpcCallObserver, ct =>
                {
                    DelayFor100MillisecondsAction(ct);
                    throw exception;
                },
                RetryDuration, clientOperationMetricsBuilder, CancellationToken.None)).Throw<Exception>();

            // Assert
            var metric = rpcCallObserver.RpcCallMetrics.Should().ContainSingle().Subject;

            ThenRpcCallMetricsShouldBeFailed(metric, TimeSpan.Zero, exception, false);

            var attempt = metric.Attempts.Should().ContainSingle().Subject;
            ThenAttemptShouldHaveFailed(attempt, exception);

            var clientOperationMetrics = clientOperationMetricsBuilder.Build();
            ThenClientOperationMetricsShouldContainRpcCallMetrics(clientOperationMetrics, metric);
        }

        private static async Task ExecuteWithRetries(
            ITentacleClientObserver tentacleClientObserver, 
            Func<CancellationToken, Guid> action, 
            TimeSpan retryDuration,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            var sut = RpcCallExecutorFactory.Create(retryDuration, tentacleClientObserver);

            await sut.ExecuteWithRetries(
                RpcCallName,
                action,
                Substitute.For<ILog>(),
                false,
                clientOperationMetricsBuilder,
                cancellationToken);
        }

        private static Guid ExecuteResult(
            ITentacleClientObserver tentacleClientObserver,
            Func<CancellationToken, Guid> action,
            TimeSpan retryDuration,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            var sut = RpcCallExecutorFactory.Create(retryDuration, tentacleClientObserver);

            return sut.Execute(
                RpcCallName,
                action,
                clientOperationMetricsBuilder,
                cancellationToken);
        }

        private static void ExecuteVoid(
            ITentacleClientObserver tentacleClientObserver,
            Action<CancellationToken> action,
            TimeSpan retryDuration, 
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            var sut = RpcCallExecutorFactory.Create(retryDuration, tentacleClientObserver);

            sut.Execute(
                RpcCallName,
                action,
                clientOperationMetricsBuilder,
                cancellationToken);
        }

        private static Guid DelayFor100MillisecondsAction(CancellationToken cancellationToken)
        {
            Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).Wait(cancellationToken);

            return Guid.NewGuid();
        }
        
        private static void ThenRpcCallMetricsShouldBeSuccessful(RpcCallMetrics metric, TimeSpan expectedRetryDuration, bool expectedWithRetries)
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

        private static void ThenRpcCallMetricsShouldBeFailed(RpcCallMetrics metric, TimeSpan expectedRetryDuration, Exception expectedException, bool expectedWithRetries)
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

        private static void ThenClientOperationMetricsShouldContainRpcCallMetrics(
            ClientOperationMetrics clientOperationMetrics, 
            RpcCallMetrics expectedRpcCallMetrics)
        {
            var rpcCall = clientOperationMetrics.RpcCalls.Should().ContainSingle().Subject;
            rpcCall.Should().BeEquivalentTo(expectedRpcCallMetrics);
        }
    }
}