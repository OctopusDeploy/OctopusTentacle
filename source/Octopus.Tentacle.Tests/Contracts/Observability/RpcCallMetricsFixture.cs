using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Tests.Contracts.Observability
{
    [TestFixture]
    public class RpcCallMetricsFixture
    {
        private const string RpcCallName = "GetStatus";
        private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow;
        private static readonly DateTimeOffset End = Start.AddSeconds(1);
        private static readonly TimeSpan RetryDuration = TimeSpan.FromMinutes(1);

        [Test]
        public void AdditionalTimeFromRetries_ShouldBeZero_WithSingleSuccessfulAttempt()
        {
            var attempts = new[] {TimedOperation.Success(Start)};

            var sut = new RpcCallMetrics(
                RpcCallName,
                Start,
                End,
                RetryDuration,
                null,
                false,
                attempts);

            sut.AdditionalTimeFromRetries.Should().Be(TimeSpan.Zero);
        }

        [Test]
        public void AdditionalTimeFromRetries_ShouldBeSumOfAllAttemptsExceptSuccess_WithMultipleAttemptsWithSuccess()
        {
            var exception = new HalibutClientException("An error has occurred.");
            var attempts = new[]
            {
                TimedOperation.Failure(Start.Subtract(TimeSpan.FromMinutes(2)), exception, CancellationToken.None),
                TimedOperation.Failure(Start.Subtract(TimeSpan.FromMinutes(3)), exception, CancellationToken.None),
                TimedOperation.Success(Start.Subtract(TimeSpan.FromMinutes(4)))

            };

            var sut = new RpcCallMetrics(
                RpcCallName,
                Start,
                End,
                RetryDuration,
                null,
                false,
                attempts);

            var expectedDuration = attempts[0].Duration + attempts[1].Duration;
            sut.AdditionalTimeFromRetries.Should().Be(expectedDuration);
        }

        [Test]
        public void AdditionalTimeFromRetries_ShouldBeSumOfAllAttemptsAfterFirstFailure_WithMultipleAttemptsThatAllFailed()
        {
            var exception = new HalibutClientException("An error has occurred.");
            var attempts = new[]
            {
                TimedOperation.Failure(Start.Subtract(TimeSpan.FromMinutes(2)), exception, CancellationToken.None),
                TimedOperation.Failure(Start.Subtract(TimeSpan.FromMinutes(3)), exception, CancellationToken.None),
                TimedOperation.Failure(Start.Subtract(TimeSpan.FromMinutes(4)), exception, CancellationToken.None)

            };

            var sut = new RpcCallMetrics(
                RpcCallName,
                Start,
                End,
                RetryDuration,
                null,
                false,
                attempts);

            var expectedDuration = attempts[1].Duration + attempts[2].Duration;
            sut.AdditionalTimeFromRetries.Should().Be(expectedDuration);
        }
    }
}
