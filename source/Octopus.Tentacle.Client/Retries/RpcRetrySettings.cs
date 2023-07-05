using System;

namespace Octopus.Tentacle.Client.Retries
{
    public record RpcRetrySettings(bool RetriesEnabled, TimeSpan RetryDuration)
    {
        public bool RetriesEnabled { get; } = RetriesEnabled;
        public TimeSpan RetryDuration { get; } = RetryDuration;
    }
}
