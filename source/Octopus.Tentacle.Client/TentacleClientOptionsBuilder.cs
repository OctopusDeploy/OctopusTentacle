using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Util;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client
{
    public class TentacleClientOptionsBuilder
    {
        AsyncHalibutFeature asyncHalibutFeature = AsyncHalibutFeature.Disabled;
        RpcRetrySettings rpcRetrySettings = new(false, TimeSpan.Zero);
        readonly HashSet<string> disabledServices = new();

        public TentacleClientOptionsBuilder DisableService(string serviceName)
        {
            if (serviceName.Equals(nameof(IScriptService)))
                throw new ArgumentException("IScriptService cannot be disabled as it's the minimum version available on all Tentacles.", nameof(serviceName));

            disabledServices.Add(serviceName);
            return this;
        }

        public TentacleClientOptionsBuilder EnableService(string serviceName)
        {
            disabledServices.Remove(serviceName);
            return this;
        }

        internal TentacleClientOptionsBuilder SetAsyncHalibut(AsyncHalibutFeature asyncHalibut)
        {
            this.asyncHalibutFeature = asyncHalibut;
            return this;
        }

        internal TentacleClientOptionsBuilder EnableAsyncHalibut() => SetAsyncHalibut(AsyncHalibutFeature.Enabled);
        internal TentacleClientOptionsBuilder DisableAsyncHalibut() => SetAsyncHalibut(AsyncHalibutFeature.Disabled);

        internal TentacleClientOptionsBuilder SetRcRetrySettings(RpcRetrySettings settings)
        {
            rpcRetrySettings = settings;
            return this;
        }

        internal TentacleClientOptions Build() => new(disabledServices.ToHashSet(), asyncHalibutFeature, rpcRetrySettings);
    }
}