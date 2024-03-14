using System;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    public class RpcRetrySettingsBuilder
    {
        bool retriesEnabled;
        TimeSpan retryDuration = TimeSpan.Zero;

        public RpcRetrySettingsBuilder WithRetriesEnabled(bool retriesEnabled)
        {
            this.retriesEnabled = retriesEnabled;
            return this;
        }

        public RpcRetrySettingsBuilder WithRetriesEnabled() => WithRetriesEnabled(true);

        public RpcRetrySettingsBuilder WithRetriesDisabled() => WithRetriesEnabled(false);

        public RpcRetrySettingsBuilder WithRetryDuration(TimeSpan retryDuration)
        {
            this.retryDuration = retryDuration;
            return this;
        }

        public RpcRetrySettings Build() =>
            new(retriesEnabled, retryDuration);

        public static RpcRetrySettings Default() => new RpcRetrySettingsBuilder().Build();
    }
}