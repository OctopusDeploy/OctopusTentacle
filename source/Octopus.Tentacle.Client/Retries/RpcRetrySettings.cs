using System;

namespace Octopus.Tentacle.Client.Retries
{
    public record RpcRetrySettings(TimeSpan RetryDuration)
    {
        public bool RetriesEnabled = false;
        public TimeSpan RetryDuration { get; private set; } = RetryDuration;
    }
}
