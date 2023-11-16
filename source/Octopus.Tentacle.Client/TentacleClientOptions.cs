using System;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client
{
    public class TentacleClientOptions
    {
        public RpcRetrySettings RpcRetrySettings { get; }
        
        /// <summary>
        /// Disables the use of ScriptServiceV3Alpha, even if it's supported on the Tentacle
        /// </summary>
        public bool DisableScriptServiceV3Alpha { get; set; }

        public TentacleClientOptions(RpcRetrySettings rpcRetrySettings)
        {
            RpcRetrySettings = rpcRetrySettings;
        }
    }
}