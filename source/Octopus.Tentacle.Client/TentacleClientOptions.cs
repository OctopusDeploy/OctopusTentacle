using System;
using System.Collections.Generic;
using Halibut.Util;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client
{
    record TentacleClientOptions(HashSet<string> DisabledScriptServices, AsyncHalibutFeature AsyncHalibutFeature, RpcRetrySettings RpcRetrySettings)
    {
        public HashSet<string> DisabledScriptServices { get; } = DisabledScriptServices;
        public AsyncHalibutFeature AsyncHalibutFeature { get; } = AsyncHalibutFeature;
        public RpcRetrySettings RpcRetrySettings { get; } = RpcRetrySettings;
    }
}