using System;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client
{
    public class TentacleClientOptions
    {
        public RpcRetrySettings RpcRetrySettings { get; }
        
        public int? MinimumAttemptsForInterruptedLongRunningCalls { get; }

        public TentacleClientOptions(RpcRetrySettings rpcRetrySettings, int? minimumAttemptsForInterruptedLongRunningCalls = null)
        {
            RpcRetrySettings = rpcRetrySettings;
            MinimumAttemptsForInterruptedLongRunningCalls = minimumAttemptsForInterruptedLongRunningCalls;
        }
    }
}