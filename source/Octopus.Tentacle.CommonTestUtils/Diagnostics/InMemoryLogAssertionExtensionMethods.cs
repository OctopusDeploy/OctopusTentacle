using System;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Octopus.Tentacle.CommonTestUtils.Diagnostics
{
    public static class InMemoryLogAssertionExtensionMethods
    {
        static readonly Regex retryingMessageRegex = new("An error occurred communicating with Tentacle. This action will be retried after \\d* seconds. Retry attempt \\d*. Retries will be performed for up to \\d* seconds.\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex timeoutAfterRetriesMessageRegex = new("Could not communicate with Tentacle after \\d* seconds. No more retries will be attempted.\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex timeoutAfterNoRetriesMessageRegex = new("Could not communicate with Tentacle after \\d* seconds.\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        public static void ShouldHaveLoggedRetryAttemptsAndRetryFailure(this InMemoryLog logs)
        {
            AssertRetriedLogMessages(logs);
            AssertTimeoutAfterRetriedLogMessages(logs);

            // Negative assertions
            AssertNoTimeoutAfterNoRetriedLogMessages(logs);
        }

        public static void ShouldNotHaveLoggedRetryAttemptsOrRetryFailures(this InMemoryLog logs)
        {
            // Negative assertions
            AssertNoRetriedLogMessages(logs);
            AssertNoTimeoutAfterRetriedLogMessages(logs);
            AssertNoTimeoutAfterNoRetriedLogMessages(logs);
        }

        public static void ShouldHaveLoggedRetryAttemptsAndNoRetryFailures(this InMemoryLog logs)
        {
            AssertRetriedLogMessages(logs);

            // Negative assertions
            AssertNoTimeoutAfterRetriedLogMessages(logs);
            AssertNoTimeoutAfterNoRetriedLogMessages(logs);
        }

        public static void ShouldHaveLoggedRetryFailureAndNoRetryAttempts(this InMemoryLog logs)
        {
            AssertTimeoutAfterNoRetriedLogMessages(logs);

            // Negative assertions
            AssertNoRetriedLogMessages(logs);
            AssertNoTimeoutAfterRetriedLogMessages(logs);
        }

        private static void AssertRetriedLogMessages(InMemoryLog logs)
        {
            logs.GetLog().Should().MatchRegex(retryingMessageRegex);

            var matches = retryingMessageRegex.Matches(logs.GetLog());
            matches.Count.Should().BeGreaterThanOrEqualTo(1);
        }

        private static void AssertNoRetriedLogMessages(InMemoryLog logs)
        {
            logs.GetLog().Should().NotMatchRegex(retryingMessageRegex);
        }

        private static void AssertTimeoutAfterRetriedLogMessages(InMemoryLog logs)
        {
            logs.GetLog().Should().MatchRegex(timeoutAfterRetriesMessageRegex);

            var matches = timeoutAfterRetriesMessageRegex.Matches(logs.GetLog());
            matches.Count.Should().Be(1);
        }

        private static void AssertNoTimeoutAfterRetriedLogMessages(InMemoryLog logs)
        {
            logs.GetLog().Should().NotMatchRegex(timeoutAfterRetriesMessageRegex);
        }

        private static void AssertTimeoutAfterNoRetriedLogMessages(InMemoryLog logs)
        {
            logs.GetLog().Should().MatchRegex(timeoutAfterNoRetriesMessageRegex);

            var matches = timeoutAfterNoRetriesMessageRegex.Matches(logs.GetLog());
            matches.Count.Should().Be(1);
        }

        private static void AssertNoTimeoutAfterNoRetriedLogMessages(InMemoryLog logs)
        {
            logs.GetLog().Should().NotMatchRegex(timeoutAfterNoRetriesMessageRegex);
        }
    }
}
