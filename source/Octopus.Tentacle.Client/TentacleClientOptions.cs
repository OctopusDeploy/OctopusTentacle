using System;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client
{
    public class TentacleClientOptions
    {
        public RpcRetrySettings RpcRetrySettings { get; }
        
        public TentacleClientOptions(RpcRetrySettings rpcRetrySettings)
        {
            RpcRetrySettings = rpcRetrySettings;
        }
    }
}