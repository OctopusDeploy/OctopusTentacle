using System;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    public class TentacleClientOptionsBuilder
    {
        RpcRetrySettings? rpcRetrySettings;
        bool disableScriptServiceV3Alpha;

        public TentacleClientOptionsBuilder WithRpcRetrySettings(RpcRetrySettings rpcRetrySettings)
        {
            this.rpcRetrySettings = rpcRetrySettings;
            return this;
        }

        public TentacleClientOptionsBuilder WithRpcRetrySettings(Func<RpcRetrySettingsBuilder, RpcRetrySettingsBuilder> builder)
        {
            rpcRetrySettings = builder(new RpcRetrySettingsBuilder()).Build();
            return this;
        }

        public TentacleClientOptionsBuilder WithDisableScriptServiceV3Alpha(bool disableScriptServiceV3Alpha)
        {
            this.disableScriptServiceV3Alpha = disableScriptServiceV3Alpha;
            return this;
        }

        public TentacleClientOptions Build() =>
            new(rpcRetrySettings ?? RpcRetrySettingsBuilder.Default())
            {
                DisableScriptServiceV3Alpha = disableScriptServiceV3Alpha
            };

        public static TentacleClientOptions Default() => new TentacleClientOptionsBuilder().Build();
    }
}