using System;
using System.Collections.Generic;
using Halibut.Util;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client
{
    record TentacleClientOptions(HashSet<string> DisabledServices, AsyncHalibutFeature AsyncHalibutFeature, RpcRetrySettings RpcRetrySettings)
    {
        public HashSet<string> DisabledServices { get; } = DisabledServices;
        public AsyncHalibutFeature AsyncHalibutFeature { get; } = AsyncHalibutFeature;
        public RpcRetrySettings RpcRetrySettings { get; } = RpcRetrySettings;
    }
}